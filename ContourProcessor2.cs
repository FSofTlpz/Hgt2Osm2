using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

#pragma warning disable 659, 661

namespace Hgt2Osm {
   class ContourProcessor2 {

      /// <summary>
      /// Anzahl der Datenzeilen
      /// </summary>
      public int Height { get; private set; }
      /// <summary>
      /// Anzahl der Datenspalten
      /// </summary>
      public int Width { get; private set; }
      /// <summary>
      /// kleinster Wert
      /// </summary>
      public int Minimum { get; private set; }
      /// <summary>
      /// größter Wert
      /// </summary>
      public int Maximum { get; private set; }
      /// <summary>
      /// linker Rand in Grad
      /// </summary>
      public int Left { get; private set; }
      /// <summary>
      /// unterer Rand in Grad
      /// </summary>
      public int Bottom { get; private set; }

      /// <summary>
      /// Name der Ausgabedatei; Standard: cl{0}_{1}_{2}.osm, Bottom, Left, betweenpoints
      /// </summary>
      public string Outfile { get; set; }

      /// <summary>
      /// Ausgabe aller Höhenpunkte
      /// </summary>
      public double[] ShowPoints { get; set; }

      /// <summary>
      /// Ausgabe aller Höhenpunkte als Flächen
      /// </summary>
      public double[] ShowAreas { get; set; }

      /// <summary>
      /// max. Threadanzahl (sollte sinnvollerweise nicht höher als die CPU-Anzahl sein); Standard: Prozessoranzahl
      /// </summary>
      public int MaxThreads { get; private set; }

      /// <summary>
      /// 1. verwendete OSM-ID (Standard 1.000.000.000)
      /// </summary>
      public long FirstID { get; set; }
      /// <summary>
      /// letzte verwendete OSM-ID
      /// </summary>
      public long LastID { get; private set; }

      /// <summary>
      /// Soll ein Bound-Tag eingefügt werden?
      /// </summary>
      public bool OSMBound { get; set; }
      /// <summary>
      /// Soll ein Bounds-Tag eingefügt werden?
      /// </summary>
      public bool OSMBounds { get; set; }
      /// <summary>
      /// Soll ein Visible-Tag (mit 'true') eingefügt werden?
      /// </summary>
      public bool OSMVisible { get; set; }
      /// <summary>
      /// Soll ein User-Tag eingefügt werden?
      /// </summary>
      public string OSMUser { get; set; }
      /// <summary>
      /// Soll ein Version-Tag (größer 0) eingefügt werden?
      /// </summary>
      public uint OSMVersion { get; set; }
      /// <summary>
      /// Soll ein Timestamp-Tag eingefügt werden?
      /// </summary>
      public bool OSMTimestamp { get; set; }

      /// <summary>
      /// Originaldaten
      /// </summary>
      HGTReader hgt;

      /// <summary>
      /// Höhenabstand der Höhenlinien
      /// </summary>
      int ContourDistance;

      /// <summary>
      /// Lock-Objekt für das Schreiben auf die Konsole
      /// </summary>
      object consolewritelocker;

      /// <summary>
      /// Typ einer Isohypse (zur Kennzeichnung in der OSM-Datei)
      /// </summary>
      enum IsohypsenTyp {
         Nothing, Minor, Medium, Major
      }

      #region Hilfsklassen für die Berechnung der Höhenlinien

      /// <summary>
      /// Pseudo-Punkt, der durch 2 Indexe bzw. Zwischenwerte zwischen Indexen definiert ist
      /// </summary>
      class PseudoPoint : IComparable {

         public double X {
            get;
         }

         public double Y {
            get;
         }

         public bool IsValid { get; set; }


         public PseudoPoint(double x, double y) {
            X = x;
            Y = y;
         }

         public PseudoPoint(PseudoPoint p) :
            this(p.X, p.Y) { }


         /// <summary>
         /// liefert einen Punkt zwischen dem aktuellen Punkt und p
         /// <para>Wenn f == 0 ist der neue Punkt identisch zum aktuellen Punkt.</para>
         /// <para>Wenn f == 1 ist der neue Punkt identisch zu p.</para>
         /// </summary>
         /// <param name="p"></param>
         /// <param name="f"></param>
         /// <returns></returns>
         public PseudoPoint BetweenPoint(PseudoPoint p, double f) {
            return new PseudoPoint(X + f * (p.X - X), Y + f * (p.Y - Y));
         }

         /// <summary>
         /// Quadrat der Länge vom Punkt zum Koordinatenursprung
         /// </summary>
         /// <returns></returns>
         public double SquareAbsolute() {
            return X * X + Y * Y;
         }

         /// <summary>
         /// Skalarprodukt
         /// </summary>
         /// <param name="p"></param>
         /// <returns></returns>
         public double DotProduct(PseudoPoint p) {
            return X * p.X + Y * p.Y;
         }

         public override bool Equals(System.Object obj) {
            if (obj == null)
               return false;

            PseudoPoint p = obj as PseudoPoint;
            if (p == null)
               return false;

            return X == p.X && Y == p.Y;
         }

         public bool Equals(PseudoPoint p) {
            if (p == null)
               return false;

            return X == p.X && Y == p.Y;
         }

         public static PseudoPoint operator *(PseudoPoint a, double f) {
            return new PseudoPoint(a.X * f, a.Y * f);
         }

         public static PseudoPoint operator +(PseudoPoint a, PseudoPoint b) {
            return new PseudoPoint(a.X + b.X, a.Y + b.Y);
         }

         public static PseudoPoint operator -(PseudoPoint a, PseudoPoint b) {
            return new PseudoPoint(a.X - b.X, a.Y - b.Y);
         }

         public static bool operator ==(PseudoPoint a, PseudoPoint b) {
            if (ReferenceEquals(a, b))
               return true;

            if (((object)a == null) || ((object)b == null))
               return false;

            return a.X == b.X && a.Y == b.Y;
         }

         public static bool operator !=(PseudoPoint a, PseudoPoint b) {
            return !(a == b);
         }

         //public static bool operator <(PseudoPoint a, PseudoPoint b) {
         //   return a.CompareTo(b) < 0;
         //}

         //public static bool operator >(PseudoPoint a, PseudoPoint b) {
         //   return a.CompareTo(b) > 0;
         //}

         /// <summary>
         /// Vergleich (zuerst die X-Komponente, dann notfalls noch die Y-Komponente)
         /// </summary>
         /// <param name="obj"></param>
         /// <returns></returns>
         public int CompareTo(object obj) {
            if (obj == null)
               return 1;

            PseudoPoint pp = obj as PseudoPoint;
            if (pp != null) {
               int cmp = X.CompareTo(pp.X);
               if (cmp != 0)
                  return cmp;
               return Y.CompareTo(pp.Y);
            } else
               throw new ArgumentException("Object is not a PseudoPoint");
         }

         public override string ToString() {
            return string.Format("[{0}, {1}, {2}]", X, Y, IsValid);
         }

      }

      class PseudoPolyline {

         /// <summary>
         /// Punktliste
         /// </summary>
         public List<PseudoPoint> P { get; }

         /// <summary>
         /// Startpunkt
         /// </summary>
         public PseudoPoint FirstPoint {
            get {
               return P.Count > 0 ? P[0] : null;
            }
         }

         /// <summary>
         /// Endpunkt
         /// </summary>
         public PseudoPoint LastPoint {
            get {
               return P.Count > 0 ? P[P.Count - 1] : null;
            }
         }


         public PseudoPolyline() {
            P = new List<PseudoPoint>();
         }

         /// <summary>
         /// erzeugt eine Kopie
         /// </summary>
         /// <param name="ppl"></param>
         public PseudoPolyline(PseudoPolyline ppl) : this() {
            P.AddRange(ppl.P);
         }

         /// <summary>
         /// erzeugt eine <see cref="PseudoPolyline"/> aus 2 Punkten
         /// </summary>
         /// <param name="p1"></param>
         /// <param name="p2"></param>
         public PseudoPolyline(PseudoPoint p1, PseudoPoint p2) : this() {
            P.Add(p1);
            P.Add(p2);
         }


         /// <summary>
         /// Wenn möglich, werden die beiden Linien verbunden. Dazu müssen sie einen gemeinsamen Randpunkt haben. 
         /// <para>Es erfolgt KEINE ringförmige Verknüpfung.</para> 
         /// <para>Eine oder beide Linien dürfen auch "leer" sein.</para>
         /// <para>Ist ein Index angegeben, wird er für diese <see cref="PseudoPolyline"/> akt. und die angegebene <see cref="PseudoPolyline"/> wird entfernt.</para>
         ///
         /// </summary>
         /// <param name="ppl"></param>
         /// <param name="index">wenn ungleich null, dann wird der Index bei Bedarf auch akt.</param>
         /// <returns>true, wenn die Verbindung erfolgte</returns>
         public bool Concat(PseudoPolyline ppl, PseudoPolylineIndex index = null) {
            bool concat = false;
            if (!Equals(ppl)) // nicht mit sich selbst verketten
               if (P.Count > 0 &&
                   ppl.P.Count > 0) {
                  if (LastPoint == ppl.FirstPoint) {
                     if (index != null) {
                        index.Remove(ppl); // angehängte Linie aus dem Index entfernen (falls vorhanden)
                        index.Change(this, false, ppl.LastPoint); // Index der akt. Linie ändern
                     }
                     RemoveLastPoint(); // Linien verketten
                     P.AddRange(ppl.P);
                     concat = true;
                  } else if (LastPoint == ppl.LastPoint) {
                     if (index != null) {
                        index.Remove(ppl);
                        index.Change(this, false, ppl.FirstPoint);
                     }
                     RemoveLastPoint();
                     P.AddRange(ppl.Invert().P);
                     concat = true;
                  } else if (FirstPoint == ppl.LastPoint) {
                     if (index != null) {
                        index.Remove(ppl);
                        index.Change(this, true, ppl.FirstPoint);
                     }
                     RemoveFirstPoint();
                     P.InsertRange(0, ppl.P);
                     concat = true;
                  } else if (FirstPoint == ppl.FirstPoint) {
                     if (index != null) {
                        index.Remove(ppl);
                        index.Change(this, true, ppl.LastPoint);
                     }
                     RemoveFirstPoint();
                     P.InsertRange(0, ppl.Invert().P);
                     concat = true;
                  }
               } else {
                  if (ppl.P.Count == 0)
                     concat = true;
                  else {
                     if (index != null) {
                        index.Remove(ppl);
                        index.Remove(this);
                     }
                     P.AddRange(ppl.P);
                     if (index != null)
                        index.Add(this);
                     concat = true;
                  }
               }
            return concat;
         }

         /// <summary>
         /// entfernt den ersten Punkt
         /// </summary>
         void RemoveFirstPoint() {
            P.RemoveAt(0);
         }

         /// <summary>
         /// entfernt den letzten Punkt
         /// </summary>
         void RemoveLastPoint() {
            P.RemoveAt(P.Count - 1);
         }

         /// <summary>
         /// liefert die invertierte Linie
         /// </summary>
         /// <returns></returns>
         public PseudoPolyline Invert() {
            PseudoPolyline ppl = new PseudoPolyline();
            for (int i = P.Count - 1; i >= 0; i--)
               ppl.P.Add(P[i]);
            return ppl;
         }

         /// <summary>
         /// ermittelt die Größe des umschließenden Rechtecks
         /// </summary>
         /// <param name="left"></param>
         /// <param name="top"></param>
         /// <param name="width"></param>
         /// <param name="height"></param>
         public void BoundingRectangle(out double left, out double top, out double width, out double height) {
            double wmin = double.MaxValue,
                   wmax = double.MinValue,
                   hmin = double.MaxValue,
                   hmax = double.MinValue;
            for (int i = 0; i < P.Count; i++) {
               wmin = Math.Min(wmin, P[i].X);
               wmax = Math.Max(wmin, P[i].X);
               hmin = Math.Min(wmin, P[i].Y);
               hmax = Math.Max(wmin, P[i].Y);
            }
            left = wmin;
            top = hmax;
            width = wmax - wmin;
            height = hmax - hmin;
         }

         /// <summary>
         /// Douglas-Peucker-Algorithmus
         /// </summary>
         /// <param name="dWidth"></param>
         /// <returns>Anzahl der gelöschten Punkte</returns>
         public int DouglasPeucker(double dWidth) {
            int iCount = P.Count;
            if (FirstPoint == LastPoint) { // wenn geschlossen
               P[iCount - 1].IsValid = true;
               iCount--;
            }

            if (iCount <= 2)
               return 0;

            for (int i = 0; i < iCount; i++) // zunächst alle Punkt ungültig
               P[i].IsValid = false;
            P[0].IsValid =                   // 1. und letzter Punkt sind immer gültig
            P[iCount - 1].IsValid = true;

            DouglasPeuckerRecursive(0, iCount - 1, dWidth * dWidth);

            return RemoveNotValidPoints();
         }

         /// <summary>
         /// Wenn ein Punkt der Polylinie von iStart bis iEnd seitlich zu weit von der Verbindung von iStart zu iEnd entfernt ist, wird er
         /// als gültig gesetzt und die Polylinie an dieser Stelle geteilt. Die Teil-Polylinien werden (rekursiv) genauso weiter untersucht.
         /// Zum Schluß sind alle notwendigen Punkte gültig gesetzt. Die anderen können entfernt werden.
         /// </summary>
         /// <param name="iStart">Index des 1. Punktes</param>
         /// <param name="iEnd">Index des letzten Punktes</param>
         /// <param name="dSquareWidth">Quadrat der min. Abweichung</param>
         void DouglasPeuckerRecursive(int iStart, int iEnd, double dSquareWidth) {
            int idx = GetFarPointIdx4DouglasPeucker(iStart, iEnd, dSquareWidth);
            if (idx > 0) {                // Aufteilung der Polylinie, weil der Trennpunkt seitlich zu weit weg von der Verbindung zwischen Anfangs- und Endpunkt liegt
               P[idx].IsValid = true;     // Trennpunkt ist auf jeden Fall gültig
               if (idx - iStart > 1)      // rekursiv für die 1. Teil-Polylinie
                  DouglasPeuckerRecursive(iStart, idx, dSquareWidth);
               if (iEnd - idx > 1)        // rekursiv für die 2. Teil-Polylinie
                  DouglasPeuckerRecursive(idx, iEnd, dSquareWidth);
            }
         }

         /// <summary>
         /// Wenn ein Punkt der Polylinie von iStart bis iEnd weiter entfernt ist, wird die Polylinie an dieser Stelle geteilt und der
         /// Index des "Teilungspunktes" geliefert. Dieser Punkt bleibt in der Polylinie erhalten.
         /// </summary>
         /// <param name="iStart">Index des 1. Punktes der untersuchten (Teil-)Polylinie</param>
         /// <param name="iEnd">Index des letzten Punktes der untersuchten (Teil-)Polylinie</param>
         /// <param name="dMinSquareWidth">Quadrat des min. nötigen Abstandes für einen "Teilungspunkt"</param>
         /// <returns>Index des Teilungspunktes oder negativ</returns>
         int GetFarPointIdx4DouglasPeucker_old(int iStart, int iEnd, double dMinSquareWidth) {
            int idx = -1;
            PseudoPoint pBaseLine = P[iEnd] - P[iStart];    // Verbindung von Anfangs- und Endpunkt (Richtungsvektor)
            double dSquare_AbsBaseLine = pBaseLine.SquareAbsolute();    // Quadrat der Länge der Verbindung von Anfangs- und Endpunkt

            for (int i = iStart + 1; i < iEnd; i++) {

               /* Für die Strecke AB mit dem Winkel alpha im Punkt A zum Punkt P und dem Fußpunkt F des Punktes P auf AB ergibt sich:
                * 
                *    cos(alpha) = |AF| / |AP|
                *    |AF| = |AP| * cos(alpha)
                * 
                * Außerdem gilt im rechtwinkligen Dreieck:
                * 
                *    |AP|² = |AF|² + |FP|²
                *    
                * Mit |FP| = d folgt:
                * 
                *    d² = |AP|² - |AF|²
                *    d² = |AP|² - (|AP| * cos(alpha))²
                *    d² = |AP|² - |AP|² * (cos(alpha))²
                *    
                * Gleichzeitig gilt für das Skalarprodukt von AP und AB:
                * 
                *    AP * AB = |AP| * |AB| * cos(alpha)
                *    cos(alpha) = (AP * AB) / (|AP| * |AB|)
                * 
                *    d² = |AP|² - |AP|² * ((AP * AB) / (|AP| * |AB|))²
                *    d² = |AP|² - (AP * AB)² / |AB|²
                * 
                * Man kann also den Abstand d mit der Länge von AP bzw. AB und dem Skalarprodukt der beiden Vektoren bestimmen.
                * 
                * Noch effektiver ist die Bestimmung des Quadrates des Abstandes.
                */
               PseudoPoint pTestLine = P[i] - P[iStart];       // Testpunkt
               double dDotProduct = pBaseLine.DotProduct(pTestLine);       // Skalarprodukt des Testpunktes zur Linie
               double dSquare_WidthTest = (dSquare_AbsBaseLine * pTestLine.SquareAbsolute() - dDotProduct * dDotProduct) / dSquare_AbsBaseLine; // Quadrat des Abstandes des Testpunktes von der Linie
               if (dMinSquareWidth < dSquare_WidthTest) { // auf jeden Fall Teilung der Polylinie nötig, aber ev. erst bei einem späteren i
                  dMinSquareWidth = dSquare_WidthTest;
                  idx = i;
               }
            }
            return idx;
         }

         /// <summary>
         /// Wenn ein Punkt der Polylinie von iStart bis iEnd weiter entfernt ist, wird die Polylinie an dieser Stelle geteilt und der
         /// Index des "Teilungspunktes" geliefert. Dieser Punkt bleibt in der Polylinie erhalten.
         /// <para>ACHTUNG</para>
         /// <para>Auch wenn der DP-Parameter sehr klein ist, werden viele Punkte entfernt, da sie von anderen Punkten mit weiterem Abstand praktisch "verdeckt"
         /// werden. Bei hinreichend kleinem DP-Parameter bringt eine weitere Verkleinerung nichts mehr.</para>
         /// </summary>
         /// <param name="iStart">Index des 1. Punktes der untersuchten (Teil-)Polylinie</param>
         /// <param name="iEnd">Index des letzten Punktes der untersuchten (Teil-)Polylinie</param>
         /// <param name="dMinSquareWidth">Quadrat des min. nötigen Abstandes für einen "Teilungspunkt"</param>
         /// <returns>Index des Teilungspunktes oder negativ</returns>
         int GetFarPointIdx4DouglasPeucker(int iStart, int iEnd, double dMinSquareWidth) {
            int idx = -1;

            PseudoPoint vAB = P[iEnd] - P[iStart];             // Verbindung von Anfangs- und Endpunkt
            double dSquareAB = vAB.SquareAbsolute();           // Quadrat der Länge der Verbindung von Anfangs- und Endpunkt (spart das Wurzelziehen)
            if (dSquareAB == 0)
               return -1;                                      // nur zur Sicherheit; sollte nicht vorkommen

            for (int i = iStart + 1; i < iEnd; i++) {

               /* Für die Fläche des durch Strecken AB und AP aufgespannten Parallelogramms ergibt sich der Flächeninhalt aus dem Vektorprodukt (bzw. des
                * Betrages des Vektorproduktes). Das Dreieck ABP hat den hablbe Flächeninhalt.
                * Gleichzeitig ergibt sich Flächeninhalt von ABP aus der Höhe d von P auf die Gerade durch A und B und der Länge von AB.
                * 
                *    |AB x AP| / 2 = d * |AB| / 2
                *    d = |AB x AP| / |AB|
                * 
                * Da das Vektorprodukt AB x AP im rechten Winkel auf der Ebene, die durch AB und AP gebildet wird, steht, ist seine Länge direkt 
                *    |ABx * APy - APx * ABy|
                * bzw.
                *    
                *    |(Bx - Ax) * (Py - Ay) - (Px - Ax) * (Ay - By)|
                *    
                * und
                *    
                *    d = |(Bx - AX) * (Py - Ay) - (Px - Ax) * (Ay - By)| / sqrt((Bx - Ax)²  + (Ay - By)²)
                *    
                * Um sich das Ziehen der Wurzel zu sparen, berechnet man d²:
                * 
                *    d² = ((Bx - AX) * (Py - Ay) - (Px - Ax) * (Ay - By))² / ((Bx - Ax)²  + (Ay - By)²)
                *    
                */

               /*    "double" in .NET:
                *    1 Bit Vorzeichen
                *    52 Bit Mantisse
                *    11 Bit Exponent
                */


               PseudoPoint vAP = P[i] - P[iStart];                // Testpunkt (-vektor)
               double ABxAP = vAB.X * vAP.Y - vAB.Y * vAP.X;      // Z-Komponente des Vektorprodukts (kann auch negativ sein!)
               double dSquareWidth = ABxAP * ABxAP / dSquareAB;

               if (dMinSquareWidth < dSquareWidth) { // auf jeden Fall Teilung der Polylinie nötig, aber ev. erst bei einem späteren i
                  dMinSquareWidth = dSquareWidth;
                  idx = i;
               }
               //else {
               //   Console.Error.WriteLine();
               //   if (iEnd - iStart == 2) {
               //      Console.Error.WriteLine();
               //   }
               //}
            }
            return idx;
         }

         /// <summary>
         /// ungültige Punkte entfernen
         /// </summary>
         /// <returns>Anzahl der gelöschten Punkte</returns>
         int RemoveNotValidPoints() {
            int removed = 0;
            for (int i = P.Count - 1; i >= 0; i--)
               if (!P[i].IsValid) {
                  P.RemoveAt(i);
                  removed++;
               }
            return removed;
         }


         public override string ToString() {
            return string.Format("{0} Punkte ({1} - {2})", P.Count, FirstPoint, LastPoint);
         }

      }

      /// <summary>
      /// ein "Sack" voll Strecken
      /// </summary>
      class PseudoLineBag {

         SortedDictionary<PseudoPoint, PseudoPoint[]> linelst; // nach Versuch hier deutlich schneller als SortedList<>


         public PseudoLineBag() {
            linelst = new SortedDictionary<PseudoPoint, PseudoPoint[]>();
         }

         /// <summary>
         /// registriert die Strecke
         /// </summary>
         /// <param name="p1"></param>
         /// <param name="p2"></param>
         public void Add(PseudoPoint p1, PseudoPoint p2) {
            PseudoPoint[] ptlst = null;
            if (linelst.TryGetValue(p1, out ptlst)) {
               if (!ptlst.Contains(p2)) {
                  if (PointlistIsFull(ptlst))
                     Add(IncreasePointlist(p1), p2);
                  else
                     Add(ptlst, p2);
               }
            } else {
               // "umdrehen"
               if (linelst.TryGetValue(p2, out ptlst)) {
                  if (!ptlst.Contains(p1)) {
                     if (PointlistIsFull(ptlst))
                        Add(IncreasePointlist(p2), p1);
                     else
                        Add(ptlst, p1);
                  }
               } else { // weder p1 noch p2 in der Liste
                  linelst.Add(p1, new PseudoPoint[] { p2, null }); // meistens wird es genau 2 Verbindungspunkte geben
               }
            }
         }

         /// <summary>
         /// vergrößert die Liste
         /// </summary>
         /// <param name="pt"></param>
         /// <returns></returns>
         PseudoPoint[] IncreasePointlist(PseudoPoint pt, int increase = 2) {
            PseudoPoint[] tmp = linelst[pt];
            PseudoPoint[] tmpnewlst = new PseudoPoint[tmp.Length + increase];
            for (int i = 0; i < tmp.Length; i++)
               tmpnewlst[i] = tmp[i];
            linelst[pt] = tmpnewlst;
            return tmpnewlst;
         }

         /// <summary>
         /// fügt einen Punkt zur Punktliste hinzu
         /// </summary>
         /// <param name="lst"></param>
         /// <param name="pt"></param>
         void Add(PseudoPoint[] lst, PseudoPoint pt) {
            for (int i = 0; i < lst.Length; i++)
               if (lst[i] == null) {
                  lst[i] = pt;
                  break;
               }
         }

         /// <summary>
         /// übernimmt alle Strecken aus dem <see cref="PseudoLineBag"/>
         /// </summary>
         /// <param name="bag"></param>
         public void Add(PseudoLineBag bag) {
            foreach (var item in bag.linelst) {
               foreach (PseudoPoint pt2 in item.Value) {
                  if (pt2 != null)
                     Add(item.Key, pt2);
               }
            }
         }

         /// <summary>
         /// Anzahl der Punkte in der Punktliste
         /// </summary>
         /// <param name="lst"></param>
         /// <returns></returns>
         int PointlistCount(PseudoPoint[] lst) {
            int c = 0;
            for (int i = 0; i < lst.Length; i++)
               if (lst[i] == null)
                  return c;
               else
                  c++;
            return c;
         }

         /// <summary>
         /// true, wenn die Punktliste voll ist
         /// </summary>
         /// <param name="lst"></param>
         /// <returns></returns>
         bool PointlistIsFull(PseudoPoint[] lst) {
            return lst[lst.Length - 1] != null;
         }

         /// <summary>
         /// entfernt alle Strecken
         /// </summary>
         public void Clear() {
            linelst.Clear();
         }

         /// <summary>
         /// Anzahl der registrierten Strecken
         /// </summary>
         /// <returns></returns>
         public int Count() {
            int lines = 0;
            foreach (var item in linelst)
               lines += PointlistCount(item.Value);
            return lines;
         }

         /// <summary>
         /// liefert eine Liste aller Strecken als <see cref="PseudoPolyline"/>
         /// </summary>
         /// <returns></returns>
         public List<PseudoPolyline> GetAll() {
            List<PseudoPolyline> lst = new List<PseudoPolyline>();
            foreach (KeyValuePair<PseudoPoint, PseudoPoint[]> item in linelst) {
               foreach (PseudoPoint p2 in item.Value) {
                  if (p2 != null)
                     lst.Add(new PseudoPolyline(item.Key, p2));
                  else
                     break;
               }
            }
            return lst;
         }

         /// <summary>
         /// liefert jedesmal eine Strecke als <see cref="PseudoPolyline"/> und entfernt sie
         /// <para>Ist keine Strecke mehr vorhanden, wird null geliefert.</para>
         /// </summary>
         /// <returns></returns>
         public PseudoPolyline Extract() {
            PseudoPolyline ppl = null;
            if (linelst.Count > 0) {
               PseudoPoint pt1 = linelst.Keys.First();
               PseudoPoint[] pt2lst = linelst[pt1];
               //KeyValuePair<PseudoPoint, PseudoPoint[]> item = linelst.First();
               if (pt2lst[0] == null)
                  throw new Exception("Fehler in PseudoPolyline.Extract().");
               else {
                  ppl = new PseudoPolyline(pt1, pt2lst[0]);
                  if (pt2lst[1] != null) {
                     for (int i = 1; i < pt2lst.Length; i++) {
                        pt2lst[i - 1] = pt2lst[i];
                     }
                     pt2lst[pt2lst.Length - 1] = null;
                  } else
                     linelst.Remove(pt1);
               }
            }
            return ppl;
         }

         public override string ToString() {
            return string.Format("Listengröße {0}", linelst.Count);
         }

      }

      /// <summary>
      /// Index für die beiden Endpunkte der <see cref="PseudoPolyline"/>
      /// </summary>
      class PseudoPolylineIndex {

         /// <summary>
         /// Liste der Liste aller <see cref="PseudoPolyline"/> je Randpunkt
         /// <para>Es wird davon ausgegangen, dass je Randpunkt nur wenige <see cref="PseudoPolyline"/> existieren.</para>
         /// </summary>
         SortedList<PseudoPoint, List<PseudoPolyline>> lst; // nach Versuch hier genauso schnelle wie SortedDictionary<>
                                                            //SortedDictionary<PseudoPoint, List<PseudoPolyline>> lst;

         /// <summary>
         /// Jede <see cref="PseudoPolyline"/> wird 2x (für den Start- und den Endpunkt) registriert!
         /// </summary>
         public int Count {
            get {
               int sum = 0;
               foreach (var item in lst)
                  sum += item.Value.Count;
               return sum;
            }
         }


         public PseudoPolylineIndex() {
            lst = new SortedList<PseudoPoint, List<PseudoPolyline>>();
            //lst = new SortedDictionary<PseudoPoint, List<PseudoPolyline>>();
         }

         /// <summary>
         /// die <see cref="PseudoPolyline"/> wird mit ihren Randpunkten registriert
         /// </summary>
         /// <param name="ppl"></param>
         public void Add(PseudoPolyline ppl) {
            Insert(ppl, ppl.FirstPoint);
            Insert(ppl, ppl.LastPoint);
         }

         void Insert(PseudoPolyline ppl, PseudoPoint pt) {
            List<PseudoPolyline> lines;
            if (!lst.TryGetValue(pt, out lines)) { // der Randpunkt ex. noch nicht
               lines = new List<PseudoPolyline>();
               lst.Add(pt, lines);
            }
            if (!lines.Contains(ppl))
               lines.Add(ppl);
         }

         /// <summary>
         /// entfernt die <see cref="PseudoPolyline"/>
         /// <para>Die <see cref="PseudoPolyline"/> wird nur entfernt, wenn sie noch die gleichen Randpunkte wie beim Einfügen hat!</para>
         /// </summary>
         /// <param name="ppl"></param>
         public void Remove(PseudoPolyline ppl) {
            Remove(ppl, ppl.FirstPoint);
            Remove(ppl, ppl.LastPoint);
         }

         void Remove(PseudoPolyline ppl, PseudoPoint pt) {
            List<PseudoPolyline> lines;
            if (lst.TryGetValue(pt, out lines)) {
               lines.Remove(ppl);
               if (lines.Count == 0)
                  lst.Remove(pt);
            }
         }

         /// <summary>
         /// ändert die Registrierung eines Randpunktes der <see cref="PseudoPolyline"/> (die <see cref="PseudoPolyline"/> muss noch den alten Randpunkt enthalten!)
         /// </summary>
         /// <param name="ppl"></param>
         /// <param name="startpoint"></param>
         /// <param name="newpt">neuer Randpunkt</param>
         public void Change(PseudoPolyline ppl, bool startpoint, PseudoPoint newpt) {
            if (startpoint)
               Remove(ppl, ppl.FirstPoint);
            else
               Remove(ppl, ppl.LastPoint);
            Insert(ppl, newpt);
         }

         /// <summary>
         /// liefert eine Liste aller <see cref="PseudoPolyline"/> mit dem gewünschten Randpunkt
         /// </summary>
         /// <param name="pt"></param>
         /// <returns></returns>
         List<PseudoPolyline> GetAllPseudoPolylines4PseudoPoint(PseudoPoint pt) {
            List<PseudoPolyline> lines;
            if (lst.TryGetValue(pt, out lines))
               return new List<PseudoPolyline>(lines); // damit eine Kopie der Liste geliefert wird (i.A. eine sehr kleine Liste)
            return null;
         }

         /// <summary>
         /// liefert eine (i.A. kleine) Liste aller <see cref="PseudoPolyline"/>, die an die <see cref="PseudoPolyline"/> passen  (ohne diese <see cref="PseudoPolyline"/> selbst)
         /// </summary>
         /// <param name="ppl"></param>
         /// <returns></returns>
         public List<PseudoPolyline> GetAllPseudoPolylines4PseudoPolyline(PseudoPolyline ppl) {
            List<PseudoPolyline> lines = GetAllPseudoPolylines4PseudoPoint(ppl.FirstPoint);
            if (lines != null) {
               lines.Remove(ppl); // falls die Polyline selber in der Liste enthalten ist, wird sie entfernt
               List<PseudoPolyline> lines2 = GetAllPseudoPolylines4PseudoPoint(ppl.LastPoint);
               if (lines2 != null && lines2.Count > 0)
                  lines.AddRange(lines2);
            } else {
               lines = GetAllPseudoPolylines4PseudoPoint(ppl.LastPoint);
            }
            if (lines != null)
               lines.Remove(ppl);
            return lines;
         }

         public string GetDebugStatus() {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(ToString());
            foreach (var item in lst) {
               sb.AppendFormat("   {0}:", item.Key);
               sb.AppendLine();
               foreach (var ppl in item.Value) {
                  sb.AppendFormat("      {0}", ppl);
                  sb.AppendLine();
               }
            }
            return sb.ToString();
         }
         /// <summary>
         /// Summe aller Punkte in den Polylinien
         /// </summary>
         /// <returns></returns>
         public int GetDebugPointCount() {
            int sum = 0;
            foreach (var item in lst)
               foreach (var item2 in item.Value)
                  sum += item2.P.Count;
            return sum;
         }


         public override string ToString() {
            return string.Format("{0} Randpunkte", lst.Count);
         }

      }

      /// <summary>
      /// ein "Sack" voll Polylinien
      /// </summary>
      class PseudoPolylineBag {

         PseudoPolylineIndex index;

         public List<PseudoPolyline> Polylines { get; }


         public PseudoPolylineBag() {
            Polylines = new List<PseudoPolyline>();
            index = new PseudoPolylineIndex();
         }

         /// <summary>
         /// die Strecken werden ohne Verknüpfung einfach übernommen
         /// </summary>
         /// <param name="linebag"></param>
         public void BuildSimplePseudoPolylines(PseudoLineBag linebag) {
            Polylines.AddRange(linebag.GetAll());
         }

         /// <summary>
         /// die Strecken werden so weit wie möglich zu <see cref="PseudoPolyline"/> verknüpft, in der <see cref="Polylines"/> gespeichert und die Strecken werden gelöscht
         /// </summary>
         public void BuildPseudoPolylines(PseudoLineBag linebag) {
            PseudoPolyline ppl;
            while ((ppl = linebag.Extract()) != null) {
               AddPolyline(ppl); // jede Strecke wird an eine Polyline angefügt oder als neue Polyline registriert
            }
         }

         /// <summary>
         /// eine <see cref="PseudoPolyline"/>  wird an eine bestehende <see cref="PseudoPolyline"/> angefügt oder als neue <see cref="PseudoPolyline"/> in die Liste <see cref="Polylines"/> aufgenommen
         /// </summary>
         /// <param name="ppl"></param>
         void AddPolyline(PseudoPolyline ppl) {
            /* Es wird davon ausgegangen, dass die bisher schon vorhandenen Polylines nicht mehr verknüpfbar sind.
             * Dann kann die hinzukommende Polyline mit max. 2 schon vorhandenen Polylines verknüpft werden.
             */

            List<PseudoPolyline> lines = index.GetAllPseudoPolylines4PseudoPolyline(ppl);
            if (lines != null &&
                lines.Count > 0) {

               if (lines.Count == 2 &&             // Sonderfall: neue Polylinie schließt die gefundene Polylinie zum Ring 
                   lines[0].Equals((lines[1]))) {  //    -> Polyline steht in Zukunft nicht mehr für Verknüpfungen zur Verfügung -> aus Index entfernen 

                  index.Remove(lines[0]);
                  lines[0].Concat(ppl);

               } else {

                  lines[0].Concat(ppl, index); // Index wird auch akt.

                  if (lines.Count > 1) {
                     index.Remove(lines[1]);
                     lines[0].Concat(lines[1], index);
                     Polylines.Remove(lines[1]);
                  }

               }

            } else { // dann nur einfügen

               index.Add(ppl);
               Polylines.Add(ppl);

            }

         }

         void AddPolyline1(PseudoPolyline ppl) {
            index.Add(ppl);
            Polylines.Add(ppl);

            //Debug.WriteLine("Add: " + ppl.ToString());
            //Debug.WriteLine(index.GetDebugStatus());

            bool found;
            do {
               found = false;
               List<PseudoPolyline> lines = index.GetAllPseudoPolylines4PseudoPolyline(ppl);
               if (lines != null && lines.Count > 0) {

                  //Debug.WriteLine("Concat: " + ppl.ToString() + " mit " + lines[0].ToString());
                  //int debuglines1 = index.Count;
                  //int debugpt1 = index.GetDebugPointCount();

                  ppl.Concat(lines[0], index);
                  Polylines.Remove(lines[0]);

                  if (ppl.FirstPoint == ppl.LastPoint) { // Polyline steht in Zukunft nicht mehr für Verknüpfungen zur Verfügung -> aus Index entfernen
                     index.Remove(ppl);

                     //debuglines1 -= 2;
                     //debugpt1 -= 2 * ppl.P.Count;
                  }

                  found = true;

                  //Debug.WriteLine(index.GetDebugStatus());
                  //int debuglines2 = index.Count;
                  //int debugpt2 = index.GetDebugPointCount();
                  //if (debuglines1 != debuglines2 + 2 ||
                  //    debugpt1 != debugpt2 + 2)
                  //   throw new Exception();

               }
            } while (found);

            //if (index.Count > 2 * Polylines.Count)
            //   throw new Exception();
         }

         /// <summary>
         /// Anzahl der geschlossenen Polylinien
         /// </summary>
         /// <returns></returns>
         public int ClosedPolylineCount() {
            int count = 0;
            foreach (var item in Polylines) {
               if (item.FirstPoint == item.LastPoint)
                  count++;
            }
            return count;
         }

         public void Test() {
            for (int i = 0; i < Polylines.Count; i++)
               for (int j = 0; j < Polylines.Count; j++)
                  if (i != j) {
                     if (Polylines[i].FirstPoint == Polylines[j].FirstPoint ||
                         Polylines[i].FirstPoint == Polylines[j].LastPoint ||
                         Polylines[i].LastPoint == Polylines[j].FirstPoint ||
                         Polylines[i].LastPoint == Polylines[j].LastPoint)
                        Console.Error.Write("Verknüpfung der Polylinien {0} und {1} fehlt!", i, j);
                  }
         }

         public override string ToString() {
            return string.Format("{0} Polylinien, Anzahl im Index {1}", Polylines.Count, index.Count);
         }

      }

      class PseudoBitmap {

         public Bitmap bm { get; }

         Graphics g;

         int maxwidth, maxheight;


         public PseudoBitmap(int size, int maxwidth, int height) {
            bm = new Bitmap(size, size);
            this.maxwidth = maxwidth;
            this.maxheight = height;
         }

         public void DrawLines(SortedDictionary<int, PseudoLineBag> linebags, string pngfilename, int colstep = 8) {
            Draw(null, linebags, colstep, pngfilename);
         }

         public void DrawPolylines(SortedDictionary<int, PseudoPolylineBag> linebags, string pngfilename, int colstep = 8) {
            Draw(linebags, null, colstep, pngfilename);
         }

         void Draw(SortedDictionary<int, PseudoPolylineBag> polylinebags, SortedDictionary<int, PseudoLineBag> linebags, int colstep, string pngfilename) {
            Console.Error.WriteLine("erzeuge Bilddatei " + pngfilename + " ...");

            if (polylinebags == null) {
               polylinebags = new SortedDictionary<int, PseudoPolylineBag>();
               foreach (var item in linebags) {
                  PseudoPolylineBag pplbag = new PseudoPolylineBag();
                  pplbag.BuildSimplePseudoPolylines(item.Value);
                  polylinebags.Add(item.Key, pplbag);
               }
            }

            g = Graphics.FromImage(bm);
            g.Clear(Color.White);

            int min = polylinebags.Keys.Min();
            int max = polylinebags.Keys.Max();

            Pen pen = new Pen(Color.DarkGray);
            PointF pt = new PointF();
            for (int i = 0; i <= maxheight; i++)
               for (int j = 0; j <= maxwidth; j++) {
                  SetPoint(ref pt, j, i);
                  //g.DrawLine(pen, pt, pt);
                  g.DrawRectangle(pen, pt.X, pt.Y, 1, 1);
               }

            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.Default;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;


            List<Color> cols = BuildColorArray(colstep);

            foreach (KeyValuePair<int, PseudoPolylineBag> item in polylinebags) {
               int idx = item.Key == max ?
                              cols.Count - 1 :
                              ((cols.Count - 1) * (item.Key - min)) / (max - min);
               Draw(item.Value, cols[idx]);
            }

            g.Flush();
            g.Dispose();

            if (!string.IsNullOrEmpty(pngfilename))
               bm.Save(pngfilename, System.Drawing.Imaging.ImageFormat.Png);
         }

         void Draw(PseudoPolylineBag bag, Color col) {
            Pen pen = new Pen(col, 0.0f);
            foreach (PseudoPolyline ppl in bag.Polylines) {
               PointF[] pt = new PointF[ppl.P.Count];
               for (int i = 0; i < ppl.P.Count; i++)
                  SetPoint(ref pt[i], ppl.P[i].X, ppl.P[i].Y);
               g.DrawLines(pen, pt);
            }
            pen.Dispose();
         }

         void SetPoint(ref PointF pt, double x, double y) {
            pt.X = (int)((bm.Width * x) / maxwidth);
            pt.Y = (int)((bm.Height * (maxheight - y)) / maxheight); // Bitmap ist von oben nach unten orientiert
         }

         List<Color> BuildColorArray(int step) {
            List<Color> colors = new List<Color>();

            // außer Schwarz und Weiß

            //for (int i = step; i < 256; i += step) {
            //   colors.Add(Color.FromArgb(0, i, 0));
            //}
            //for (int i = 0; i < 256; i += step) {
            //   colors.Add(Color.FromArgb(i, 255, 0));
            //}
            //for (int i = 0; i < 256; i += step) {
            //   colors.Add(Color.FromArgb(255, 255 - i, 0));
            //}
            //for (int i = 0; i < 256; i += step) {
            //   colors.Add(Color.FromArgb(255, 0, i));
            //}
            //for (int i = 0; i < 256; i += step) {
            //   colors.Add(Color.FromArgb(255 - i, i, 255));
            //}
            //for (int i = 0; i < 256; i += step) {
            //   colors.Add(Color.FromArgb(0, 255 - i, 255));
            //}
            //for (int i = 0; i < 255; i += step) {
            //   colors.Add(Color.FromArgb(0, 0, 255 - i));
            //}


            for (int i = step; i < 256; i += step) {
               colors.Add(Color.FromArgb(255 - i, 128, i));
            }
            for (int i = 0; i < 256; i += step) {
               colors.Add(Color.FromArgb(0, 128, 255 - i));
            }
            for (int i = 0; i < 256; i += step) {
               colors.Add(Color.FromArgb(i, 128 - i / 2, 0));
            }
            for (int i = 0; i < 256; i += step) {
               colors.Add(Color.FromArgb(255, 0, i));
            }
            for (int i = 0; i < 256; i += step) {
               colors.Add(Color.FromArgb(255 - i, 0, 255));
            }
            for (int i = 0; i < 256; i += step) {
               colors.Add(Color.FromArgb(0, 0, 255 - i));
            }

            return colors;
         }

      }

      #endregion

      static class PostProduction {

         /// <summary>
         /// Anzahl der restlichen Linien
         /// </summary>
         public static int Polylines { get; private set; }
         /// <summary>
         /// Anzahl der restlichen Punkte
         /// </summary>
         public static int Points { get; private set; }

         /// <summary>
         /// Polylinienanzahl mit zu wenig Punkten
         /// </summary>
         public static int ShortPolylines { get; private set; }
         /// <summary>
         /// Polylinienanzahl mit zu geringer Ausdehnung
         /// </summary>
         public static int SmallPolylines { get; private set; }
         /// <summary>
         /// Polylinienanzahl senkrecht
         /// </summary>
         public static int VerticalPolylines { get; private set; }
         /// <summary>
         /// Polylinienanzahl waagerecht
         /// </summary>
         public static int HorizontalPolylines { get; private set; }
         /// <summary>
         /// Polylinienanzahl gelöscht
         /// </summary>
         public static int RemovedPolylines { get; private set; }
         /// <summary>
         /// unnötige Punkte wegen gleichem Anstieg
         /// </summary>
         public static int UnnecessaryPoints { get; private set; }
         /// <summary>
         /// entfernte Punkte wegen Douglas-Peuckert
         /// </summary>
         public static int DouglasPeuckertPoints { get; private set; }


         /// <summary>
         /// alle Datensammlungen zurücksetzen
         /// </summary>
         public static void Clear() {
            Polylines =
            Points =
            ShortPolylines =
            SmallPolylines =
            VerticalPolylines =
            HorizontalPolylines =
            RemovedPolylines =
            UnnecessaryPoints =
            DouglasPeuckertPoints = 0;
         }

         public static void Run(SortedDictionary<int, PseudoPolylineBag> polybag, uint minpt, double minboxwidth, double edge, double douglaspeucker) {
            int[] height = polybag.Keys.ToArray();
            for (int i = 0; i < height.Length; i++) {
               PseudoPolylineBag ppbag = polybag[height[i]];
               for (int j = 0; j < ppbag.Polylines.Count; j++) {
                  if (Run(ppbag.Polylines[j], minpt, minboxwidth, edge, douglaspeucker))
                     ppbag.Polylines.RemoveAt(j--);
               }
            }

            if (RemovedPolylines > 0) {
               Console.Error.WriteLine(RemovedPolylines.ToString() + " Polylinien gelöscht, davon");
               if (ShortPolylines > 0)
                  Console.Error.WriteLine("   " + ShortPolylines.ToString() + " wegen zu geringer Punktanzahl");
               if (SmallPolylines > 0)
                  Console.Error.WriteLine("   " + SmallPolylines.ToString() + " wegen zu kleiner Ausdehnung");
               if (VerticalPolylines > 0)
                  Console.Error.WriteLine("   " + VerticalPolylines.ToString() + " wegen ausschließlich senkrechtem Verlauf");
               if (HorizontalPolylines > 0)
                  Console.Error.WriteLine("   " + HorizontalPolylines.ToString() + " wegen ausschließlich waagerechtem Verlauf");
            }
            if (UnnecessaryPoints > 0)
               Console.Error.WriteLine(UnnecessaryPoints.ToString() + " unnötige Punkte (gleiche Richtung) gelöscht");
            if (DouglasPeuckertPoints > 0)
               Console.Error.WriteLine(DouglasPeuckertPoints.ToString() + " unnötige Punkte (wegen Douglas-Peuckert) gelöscht");

            Console.Error.WriteLine(Polylines.ToString() + " Polylinien mit " + Points.ToString() + " Punkten übrig");

         }

         static bool Run(PseudoPolyline ppl, uint minpt, double minboxwidth, double edge, double douglaspeucker) {
            bool bRemove = false;
            bool bEdge = false;
            double left, top, height, width;

            ppl.BoundingRectangle(out left, out top, out width, out height);
            if (left + width <= edge ||
                left <= 1.0 - edge ||
                top <= edge ||
                top - height <= 1.0 - edge)
               bEdge = true; // Linie befindet sich im Randbereich

            if (!bEdge) {
               if (ppl.P.Count < minpt) { // zu wenig Punkte
                  ShortPolylines++;
                  bRemove = true;
               }

               if (!bRemove) { // zu kleine Ausdehnung
                  if (width <= minboxwidth && height <= minboxwidth) {
                     SmallPolylines++;
                     bRemove = true;
                  }
               }

               if (!bRemove) { // senkrechte Linie
                  bRemove = true;
                  for (int k = 1; k < ppl.P.Count; k++) {
                     if (ppl.P[k].X != ppl.FirstPoint.X) {
                        bRemove = false;
                        break;
                     }
                  }
                  if (bRemove)
                     VerticalPolylines++;
               }

               if (!bRemove) { // waagerechte Linie
                  bRemove = true;
                  for (int k = 1; k < ppl.P.Count; k++) {
                     if (ppl.P[k].Y != ppl.FirstPoint.Y) {
                        bRemove = false;
                        break;
                     }
                  }
                  if (bRemove)
                     HorizontalPolylines++;
               }

               if (bRemove)
                  RemovedPolylines++;

               if (!bRemove) {
                  for (int i = 2; i < ppl.P.Count; i++) {
                     double dx1 = ppl.P[i - 1].X - ppl.P[i - 2].X;
                     double dy1 = ppl.P[i - 1].Y - ppl.P[i - 2].Y;
                     double dx2 = ppl.P[i].X - ppl.P[i - 1].X;
                     double dy2 = ppl.P[i].Y - ppl.P[i - 1].Y;
                     bool bRemovePoint = false;
                     if (dx1 != 0 && dx2 != 0 &&
                         dy1 != 0 && dy2 != 0) {
                        if (dx1 * dy2 == dx2 * dy1)
                           bRemovePoint = true;
                     } else {
                        if ((dx1 == 0 && dx2 == 0) || // beide senkrecht
                            (dy1 == 0 && dy2 == 0))   // beide waagerecht
                           bRemovePoint = true;
                     }

                     if (bRemovePoint) {
                        // Anstieg gleich, d.h. P[i-1] wird nicht benötigt
                        ppl.P.RemoveAt(i - 1);
                        i--;
                        UnnecessaryPoints++;
                     }
                  }
               }
            }

            if (!bRemove) {
               DouglasPeuckertPoints += ppl.DouglasPeucker(douglaspeucker);
            }

            if (!bRemove) {
               Polylines++;
               Points += ppl.P.Count;
            }

            return bRemove;
         }

      }


      public ContourProcessor2(HGTReader hgt, uint maxthreads = 0) {
         this.hgt = hgt;
         ContourDistance = 0;

         ShowProgressTime();
         Left = hgt.Left;
         Bottom = hgt.Bottom;
         Minimum = hgt.Minimum;
         Maximum = hgt.Maximum;
         MaxThreads = maxthreads > 0 ? (int)maxthreads : Environment.ProcessorCount;
         Console.Error.WriteLine(MaxThreads == 1 ? "{0} Thread" : "{0} Threads", MaxThreads);
         Height = hgt.Rows;
         Width = hgt.Columns;
         Console.Error.WriteLine("übernehme {0}x{1} Werte für neue Matrix {2}x{3}", hgt.Rows, hgt.Columns, Height, Width);
         Console.Error.WriteLine("ungültige Originalwerte: {0} ({1:F2}%)", hgt.NotValid, (100.0 * hgt.NotValid) / (hgt.Rows * hgt.Columns));

         // Dummywerte setzen
         //ShowPoints = null;
         //ShowAreas = null;
         //AutoCloseAreaDistance = 1.5;
         //MinBoundingbox = 5;
         FirstID = LastID = 1000000000;
         Outfile = string.Format("cl{0}{1:D2}{2}{3:D3}.osm.gz",
                                 Bottom >= 0 ? "N" : "S",
                                 Bottom >= 0 ? Bottom : -Bottom,
                                 Left >= 0 ? "E" : "W",
                                 Left >= 0 ? Left : -Left);
         OSMBound = false;
         OSMBounds = false;
         OSMTimestamp = false;
         OSMUser = "";
         OSMVersion = 0;
         OSMVisible = false;

         consolewritelocker = new object();
      }

      /// <summary>
      /// Wert (Höhe) eines Punktes im HGT-Array ([0,0] ist die Ecke links unten, auf die sich auch der Name der HGT-Datei bezieht)
      /// <para>Außerhalb des gültigen Koordinatenbereiches wird immer <see cref="HGTReader.NoValue"/> geliefert.</para>
      /// </summary>
      /// <param name="y"></param>
      /// <param name="x"></param>
      /// <returns></returns>
      public int Get(int x, int y) {
         return hgt.Get4XY(x, y);
      }

      /// <summary>
      /// alle Isohypsen erzeugen
      /// </summary>
      /// <param name="diffmin">Höhe zwischen 2 benachbarten Isohypsen</param>
      /// <param name="diffmedium">Höhe zwischen 2 benachbarten Medium-Isohypsen</param>
      /// <param name="diffmajor">Höhe zwischen 2 benachbarten Major-Isohypsen</param>
      /// <param name="maxnodesperway">max. Anzahl Nodes je Way</param>
      /// <param name="writeelevationtype">Höhenlinientyp schreiben</param>
      /// <param name="fakedistance">generelle Höhenkorrektur in Metern</param>
      /// <param name="minpoints">min. Puktanzahl einer Linie</param>
      /// <param name="minboundingbox">min. Ausdehnung einer Linie in Grad</param>
      /// <param name="douglaspeucker">min. Abweichung für Douglas-Peucker</param>
      /// <param name="linebitmapwidth">wenn größer 0 wird ein Bitmap mit den Strecken mit der entsprechenden Größe erzeugt</param>
      /// <param name="polylinebitmapwidth">wenn größer 0 wird ein Bitmap mit den Polylinien mit der entsprechenden Größe erzeugt</param>
      /// <param name="textdata">wenn true, Ausgabe der Höhen als Text</param>
      public void CreateIsohypsen(short diffmin, uint diffmedium, uint diffmajor, uint maxnodesperway, bool writeelevationtype,
                                  double fakedistance, uint minpoints, double minboundingbox, double douglaspeucker,
                                  uint linebitmapwidth, uint polylinebitmapwidth, bool textdata) {
         ContourDistance = diffmin;

         if (textdata) {
            Console.Error.WriteLine("erzeuge Text-Datendatei " + Outfile + "_dat.txt ...");
            hgt.WriteToFile(Outfile + "_dat.txt");
         }

         List<SortedDictionary<int, PseudoLineBag>> lines4row = new List<SortedDictionary<int, PseudoLineBag>>();

         // alle Höhenlinien als Strecken ermitteln
         Console.Error.WriteLine("berechne alle Strecken für Höhenlinien in {0} Rechtecken mit je 4 Dreiecken ...", (hgt.Columns - 1) * (hgt.Rows - 1));
         for (int y = 0; y < Height - 1; y++) { // Height gibt die Anzahl der Datenzeilen an; die oberste Zeile stellt keine Ecke "links-unten" mehr dar
                                                // i.A. 1201 Zeilen -> Index der untersten Zeile 1200: Zeilenindex 1..1200 abfragen -> 1 => 1199, 1200 => 0
            SortedDictionary<int, PseudoLineBag> linebags = new SortedDictionary<int, PseudoLineBag>();
            for (int x = 0; x < Width - 1; x++) // Width gibt die Anzahl der Datenspalten an; die rechteste Zeile stellt keine Ecke "links-unten" mehr dar
               CalculateRectangle(linebags, x, y, fakedistance);
            lines4row.Add(linebags);
            //Console.Error.Write(".");
         }
         Console.Error.WriteLine();

         // Min. und Max. bestimmen
         int minheight = int.MaxValue;
         int maxheight = int.MinValue;
         for (int i = 0; i < lines4row.Count; i++) {
            IEnumerable<int> k = lines4row[i].Keys;
            if (k != null && k.Count() > 0) {
               minheight = Math.Min(minheight, k.Min());
               maxheight = Math.Max(maxheight, k.Max());
            }
         }
         int count = 0;
         ShowProgressTime();

         Console.Error.WriteLine("führe alle Strecken je Höhenebene eindeutig in einer Liste zusammen ...");
         SortedDictionary<int, PseudoLineBag> linesample = new SortedDictionary<int, PseudoLineBag>();
         for (int h = minheight; h <= maxheight; h += ContourDistance) { // je Höhe alles einsammeln
            PseudoLineBag samplebag = new PseudoLineBag();
            for (int i = 0; i < lines4row.Count; i++) {
               SortedDictionary<int, PseudoLineBag> linebags = lines4row[i];
               PseudoLineBag plbag;
               if (linebags.TryGetValue(h, out plbag)) {
                  samplebag.Add(plbag);
                  plbag.Clear();
               }
            }
            if (samplebag.Count() > 0) {
               linesample.Add(h, samplebag);
               count += samplebag.Count();
            }
            Console.Error.Write(".");
         }
         lines4row.Clear();
         Console.Error.WriteLine();
         ShowProgressTime();

         Console.Error.WriteLine("{0} Höhenebenen von {1} bis {2} mit insgesamt {3} Strecken gebildet ...", linesample.Count, minheight, maxheight, count);

         if (linebitmapwidth > 0)
            new PseudoBitmap((int)linebitmapwidth, Width - 1, Height - 1).DrawLines(linesample, Outfile + "_lines.png");

         // so weit wie möglich verketten (ev. multithreaded ?)
         BuildPseudoPolylinesThreadgroup tg = new BuildPseudoPolylinesThreadgroup(MaxThreads);
         tg.InfoEvent += new BuildPseudoPolylinesThreadgroup.Info(tg_InfoEvent);
         SortedDictionary<int, PseudoPolylineBag> alllinebags = new SortedDictionary<int, PseudoPolylineBag>();
         foreach (var item in linesample) {
            PseudoPolylineBag pplbag = new PseudoPolylineBag();
            alllinebags.Add(item.Key, pplbag);
            object[] para = new object[] { item.Key, item.Value, pplbag };
            tg.Start(para);
         }
         tg.NothingToDo.WaitOne();
         ShowProgressTime();

         // Test, ob wirklich alle Polylinien verbunden wurden
         //foreach (var item in alllinebags) {
         //   item.Value.Test();
         //}

         PostProduction.Clear();
         PostProduction.Run(alllinebags, minpoints, minboundingbox, 1.0 / Width, douglaspeucker);

         if (polylinebitmapwidth > 0)
            new PseudoBitmap((int)polylinebitmapwidth, Width - 1, Height - 1).DrawPolylines(alllinebags, Outfile + "_polylines.png");

         FileStream fs = new FileStream(Outfile, FileMode.Create);
         GZipStream compress = Path.GetExtension(Outfile).ToLower() == ".gz" ? new GZipStream(fs, CompressionMode.Compress) : null;
         using (StreamWriter sw = new StreamWriter(compress != null ? (Stream)compress : fs)) {
            MemoryStream memstr = new MemoryStream();

            int lines = 0;
            int points = 0;

            using (StreamWriter swway = new StreamWriter(memstr)) {
               Console.Error.WriteLine("schreibe Datei {0} ... ", Outfile);

               string sUserTags = "";
               if (OSMTimestamp)
                  sUserTags = " timestamp='" + DateTime.Now.ToString("u") + "Z'";
               if (OSMUser.Length > 0)
                  sUserTags += string.Format(" user='{0}'", OSMUser);
               if (OSMVersion > 0)
                  sUserTags += string.Format(" version='{0}'", OSMVersion);
               if (OSMVisible)
                  sUserTags += " visible='true'";

               sw.WriteLine("<?xml version='1.0' encoding='UTF-8'?>");
               sw.WriteLine("<osm version='0.6' generator='HGT2OSM'>");
               if (OSMBounds)
                  sw.WriteLine("<bounds minlat='{0}' minlon='{1}' maxlat='{2}' maxlon='{3}'/>",
                                    Bottom.ToString(CultureInfo.InvariantCulture),
                                    Left.ToString(CultureInfo.InvariantCulture),
                                    (Bottom + 1).ToString(CultureInfo.InvariantCulture),
                                    (Left + 1).ToString(CultureInfo.InvariantCulture));
               else
                  if (OSMBound)
                  sw.WriteLine("<bound box='{0},{1},{2},{3}' origin='http://dds.cr.usgs.gov/srtm/version2_1'/>",
                                    Bottom.ToString(CultureInfo.InvariantCulture),
                                    Left.ToString(CultureInfo.InvariantCulture),
                                    (Bottom + 1).ToString(CultureInfo.InvariantCulture),
                                    (Left + 1).ToString(CultureInfo.InvariantCulture));

               long actid = FirstID;
               long actlineid = FirstID;

               if (ShowPoints != null || ShowAreas != null)
                  actid = InsertOsmHeightDecoration(sw, swway, ShowPoints, ShowAreas, sUserTags, actid);

               int mink = alllinebags.Keys.Min() / diffmin;
               int maxk = alllinebags.Keys.Max() / diffmin;
               for (int k = mink; k <= maxk; k++) {
                  int height = k * diffmin;

                  if (alllinebags.ContainsKey(height)) {
                     IsohypsenTyp ityp = IsohypsenTyp.Nothing;
                     if (writeelevationtype) {
                        ityp = IsohypsenTyp.Minor;
                        if (height % diffmajor == 0)
                           ityp = IsohypsenTyp.Major;
                        else if (height % diffmedium == 0)
                           ityp = IsohypsenTyp.Medium;
                     }

                     foreach (PseudoPolyline ppl in alllinebags[height].Polylines) {
                        int linecount = WritePseudoPolyline(sw, swway, ppl, maxnodesperway, ityp, height, actid, actlineid, sUserTags);
                        actid += ppl.P.Count;
                        points += ppl.P.Count;
                        actlineid += linecount;
                        lines += linecount;
                     }
                  }
               }

               LastID = actid - 1;

               swway.Flush();

               // einlesen und ausgaben der Wege aus der temp. Datei
               memstr.Seek(0, SeekOrigin.Begin);
               using (StreamReader sr = new StreamReader(memstr)) {
                  sw.Write(sr.ReadToEnd());
               }
               sw.WriteLine("</osm>");
               memstr.Dispose();

               Console.Error.WriteLine("{0} Punkte und {1} Linien erzeugt", points, lines);

               ShowProgressTime();
            }
         }
      }


      DateTime dtStart = DateTime.MinValue;

      void ShowProgressTime() {
         if (dtStart == DateTime.MinValue) {
            dtStart = DateTime.Now;
            Console.Error.WriteLine("Startzeit: {0:d2}:{1:d2}:{2:d2}", dtStart.Hour, dtStart.Minute, dtStart.Second);
         } else {
            TimeSpan ts = DateTime.Now - dtStart;
            Console.Error.WriteLine("Laufzeit: {0:d2}:{1:d2}:{2:d2}", 24 * ts.Days + ts.Hours, ts.Minutes, ts.Seconds);
         }
      }

      /// <summary>
      /// liefert die geografische Breite zur y-Koordinate
      /// </summary>
      /// <param name="y"></param>
      /// <returns></returns>
      double Latitude(double y) {
         return Bottom + y / (Height - 1);
      }

      /// <summary>
      /// liefert die geografische Breite zur y-Koordinate als Text
      /// </summary>
      /// <param name="s"></param>
      /// <returns></returns>
      string LatitudeAsText(double y) {
         return Latitude(y).ToString(CultureInfo.InvariantCulture);
      }

      /// <summary>
      /// liefert die geografische Länge zur x-Koordinate
      /// </summary>
      /// <param name="s"></param>
      /// <returns></returns>
      double Longitude(double x) {
         return Left + x / (Width - 1);
      }

      /// <summary>
      /// liefert die geografische Länge zur x-Koordinate als Text
      /// </summary>
      /// <param name="s"></param>
      /// <returns></returns>
      string LongitudeAsText(double x) {
         return Longitude(x).ToString(CultureInfo.InvariantCulture);
      }

      /// <summary>
      /// Das Rechteck mit den (implizit) gegebenen 4 Eckpunkten mit ihrer Höhe wird in 4 Dreiecke zerlegt. Dann werden alle Höhen-Strecken berechnet.
      /// </summary>
      /// <param name="linebags"></param>
      /// <param name="left">linker Rand des Rechtecks</param>
      /// <param name="bottom">unterer Rand des Rechtecks</param>
      /// <param name="fakedistance">Verfälschung der Höhe</param>
      void CalculateRectangle(SortedDictionary<int, PseudoLineBag> linebags, int left, int bottom, double fakedistance) {
         // Das Rechteck wird in 4 Dreiecke zerlegt.
         PseudoPoint[] p = new PseudoPoint[] {
            // 4 Eckpunkte des Rechtecks
            new PseudoPoint(left, bottom + 1),
            new PseudoPoint(left + 1, bottom + 1),
            new PseudoPoint(left + 1, bottom),
            new PseudoPoint(left, bottom),

            // Mittelpunkt des Rechtecks
            new PseudoPoint(left + .5, bottom + .5),
         };

         double[] height = new double[] {
            Get(left, bottom + 1),
            Get(left + 1, bottom + 1),
            Get(left + 1, bottom),
            Get(left, bottom),

            0,
         };
         height[4] = (height[0] + height[1] + height[2] + height[3]) / 4.0;

         if (height[0] == HGTReader.NoValue ||
             height[1] == HGTReader.NoValue ||
             height[2] == HGTReader.NoValue ||
             height[3] == HGTReader.NoValue) { // genauere Untersuchung ist notwendig
            if ((height[0] == HGTReader.NoValue && height[2] == HGTReader.NoValue) || // 2 gegenüberliegende Punkte
                (height[1] == HGTReader.NoValue && height[3] == HGTReader.NoValue) || // 2 gegenüberliegende Punkte
                (height[0] == HGTReader.NoValue && height[1] == HGTReader.NoValue) || // 2 nebeneinander liegende Punkte
                (height[1] == HGTReader.NoValue && height[2] == HGTReader.NoValue) ||
                (height[2] == HGTReader.NoValue && height[3] == HGTReader.NoValue) ||
                (height[3] == HGTReader.NoValue && height[0] == HGTReader.NoValue))
               return;

            int count = 0;
            double hsum = 0;
            for (int i = 0; i < 4; i++) {
               if (height[i] != HGTReader.NoValue) {
                  hsum += height[i];
                  count++;
               }
            }
            height[4] = count > 0 ? hsum / count : HGTReader.NoValue;
         }

         //Fake, damit keine Höhen ex. die GENAU auf einer Höhenlinie liegen
         for (int i = 0; i < height.Length; i++)
            if (height[i] != HGTReader.NoValue)
               height[i] += fakedistance;

         //double add = Math.Sign(left + 1) * Math.Sign(top + 1) > 0 ? 0.1 : -0.1;
         //if (height[0] % ContourDistance == 0.0) {
         //   height[0] += add;
         //}
         //if (height[1] % ContourDistance == 0.0) {
         //   height[1] -= add;
         //}
         //if (height[2] % ContourDistance == 0.0) {
         //   height[2] += add;
         //}
         //if (height[3] % ContourDistance == 0.0) {
         //   height[3] -= add;
         //}


         //if (height[0] == 661 &&
         //   height[1] == 660 &&
         //   height[2] == 660 &&
         //   height[3] == 660) {
         //   if (Get(left, top - 2) == 661 &&
         //       Get(left + 1, top - 2) == 662) {
         //      Console.Write("");
         //   }
         //}

         CalculateTriangle(linebags, p[0], height[0], p[1], height[1], p[4], height[4]);
         CalculateTriangle(linebags, p[1], height[1], p[2], height[2], p[4], height[4]);
         CalculateTriangle(linebags, p[2], height[2], p[3], height[3], p[4], height[4]);
         CalculateTriangle(linebags, p[3], height[3], p[0], height[0], p[4], height[4]);
      }

      /// <summary>
      /// Für das Dreieck werden alle Höhenlinien ermittelt und registriert.
      /// <para>Punkt C ist immer der Mittelpunkt des Rechtecks. Die Bezeichnung der Eckpunkte verläuft im Uhrzeigersinn.</para>
      /// </summary>
      /// <param name="linebags"></param>
      /// <param name="a"></param>
      /// <param name="ha"></param>
      /// <param name="b"></param>
      /// <param name="hb"></param>
      /// <param name="c"></param>
      /// <param name="hc"></param>
      void CalculateTriangle(SortedDictionary<int, PseudoLineBag> linebags,
                             PseudoPoint a, double ha,
                             PseudoPoint b, double hb,
                             PseudoPoint c, double hc) {

         if (ha == HGTReader.NoValue ||
             hb == HGTReader.NoValue ||
             hc == HGTReader.NoValue)
            return; // kein Rechteck wegen ungültigem Eckpunkt

         /*    Es gibt prinzipiell folgende Fälle:
          *    
          *    a) Genau 1 Eckpunkt des Dreiecks hat die gesuchte Höhe. Wenn das der höchste oder niedrigste Punkt ist, gibt es keine Höhenlinie.
          *       Sonst verläuft sie zur gegenüberliegenden Seite.
          *    
          *    b) Genau 2 Eckpunkte des Dreiecks haben die gesuchte Höhe. Dann bildet die zugehörige Dreickseite die Höhenlinie.
          *    
          *    c) Alle 3 Eckpunkte liegen über oder unter der gesuchten Höhe. Es gibt keine Höhenlinie.
          *    
          *    d) (Standard) Die gesuchte Höhe wird innerhalb von genau 2 Seiten des Dreiecks erreicht. Damit ergibt sich eine Höhenlinie, die durch das Innere
          *       des Dreiecks verläuft.
          *       
          *    e) Alle 3 Eckpunkte haben die gesuchte Höhe. Das gesamte Dreieck hat also die gesuchte Höhe. Eine Dreieckseite ist dann auch eine Höhenlinie, 
          *       wenn das 2. zu dieser Seite gehörende Dreieck NICHT auch eine konstante Höhe hat. D.h. der 3. Punkt dieses 2. Dreiecks muss irgendeine andere 
          *       Höhe haben.
          */

         if (ha == hb && hb == hc) { // Spezialfall e)

            double f = ha / ContourDistance;
            if (Math.Truncate(f) == f) { // gesamtes Dreieck hat genau die Höhe einer Höhenlinie
               double opposite_ha, opposite_hb, opposite_hc;
               if (a.Y > c.Y) {
                  if (a.X < c.X) { // Dreieck oben
                     opposite_ha = Get((int)a.X, (int)a.Y - 1);
                     opposite_hb = Get((int)b.X, (int)b.Y - 1);
                     int tmph1 = Get((int)a.X, (int)a.Y + 1);
                     int tmph2 = Get((int)b.X, (int)b.Y + 1);
                     int divider = 2;
                     opposite_hc = ha + hb;
                     if (tmph1 != HGTReader.NoValue) {
                        opposite_hc += tmph1;
                        divider++;
                     }
                     if (tmph2 != HGTReader.NoValue) {
                        opposite_hc += tmph2;
                        divider++;
                     }
                     if (divider >= 3)
                        opposite_hc /= divider;
                     else
                        opposite_hc = HGTReader.NoValue;
                  } else { // Dreieck rechts
                     opposite_ha = Get((int)a.X - 1, (int)a.Y);
                     opposite_hb = Get((int)b.X - 1, (int)b.Y);
                     int tmph1 = Get((int)a.X + 1, (int)a.Y);
                     int tmph2 = Get((int)b.X + 1, (int)b.Y);
                     int divider = 2;
                     opposite_hc = ha + hb;
                     if (tmph1 != HGTReader.NoValue) {
                        opposite_hc += tmph1;
                        divider++;
                     }
                     if (tmph2 != HGTReader.NoValue) {
                        opposite_hc += tmph2;
                        divider++;
                     }
                     if (divider >= 3)
                        opposite_hc /= divider;
                     else
                        opposite_hc = HGTReader.NoValue;
                  }
               } else {
                  if (a.X < c.X) { // Dreieck links
                     opposite_ha = Get((int)a.X + 1, (int)a.Y);
                     opposite_hb = Get((int)b.X + 1, (int)b.Y);
                     int tmph1 = Get((int)a.X - 1, (int)a.Y);
                     int tmph2 = Get((int)b.X - 1, (int)b.Y);
                     int divider = 2;
                     opposite_hc = ha + hb;
                     if (tmph1 != HGTReader.NoValue) {
                        opposite_hc += tmph1;
                        divider++;
                     }
                     if (tmph2 != HGTReader.NoValue) {
                        opposite_hc += tmph2;
                        divider++;
                     }
                     if (divider >= 3)
                        opposite_hc /= divider;
                     else
                        opposite_hc = HGTReader.NoValue;
                  } else { // Dreieck unten
                     opposite_ha = Get((int)a.X, (int)a.Y + 1);
                     opposite_hb = Get((int)b.X, (int)b.Y + 1);
                     int tmph1 = Get((int)a.X, (int)a.Y - 1);
                     int tmph2 = Get((int)b.X, (int)b.Y - 1);
                     int divider = 2;
                     opposite_hc = ha + hb;
                     if (tmph1 != HGTReader.NoValue) {
                        opposite_hc += tmph1;
                        divider++;
                     }
                     if (tmph2 != HGTReader.NoValue) {
                        opposite_hc += tmph2;
                        divider++;
                     }
                     if (divider >= 3)
                        opposite_hc /= divider;
                     else
                        opposite_hc = HGTReader.NoValue;
                  }
               }

               if (opposite_ha != ha) {
                  if (!linebags.ContainsKey((int)ha))             // Höhenebene noch nicht vorhanden
                     linebags.Add((int)ha, new PseudoLineBag());
                  linebags[(int)ha].Add(a, c);
               }
               if (opposite_hb != hb) {
                  if (!linebags.ContainsKey((int)hb))             // Höhenebene noch nicht vorhanden
                     linebags.Add((int)hb, new PseudoLineBag());
                  linebags[(int)hb].Add(b, c);
               }
               if (opposite_hc != hc) {
                  if (!linebags.ContainsKey((int)hc))             // Höhenebene noch nicht vorhanden
                     linebags.Add((int)hc, new PseudoLineBag());
                  linebags[(int)hc].Add(a, b);
               }
            }

         } else {
            double hmin = Math.Min(ha, Math.Min(hb, hc));
            double hmax = Math.Max(ha, Math.Max(hb, hc));

            int fmax = (int)(hmax / ContourDistance);       // Faktor der Höhe der höchsten Höhenlinie
            int fmin = (int)(hmin / ContourDistance);
            if (fmin * ContourDistance < hmin)
               fmin++;                                      // Faktor der Höhe der tiefsten Höhenlinie

            //List<PseudoPoint> ptlst = new List<PseudoPoint>();
            PseudoPoint[] pointlist = new PseudoPoint[2];
            int pointlist_ptr = 0;

            for (int f = fmin; f <= fmax; f++) {
               int h = f * ContourDistance;                 // akt. Höhe

               if (hmin <= h && h <= hmax) {                // akt. Höhe liegt im Höhenbereich des Dreiecks (sonst Fall c))

                  if (!linebags.ContainsKey(h))             // Höhenebene noch nicht vorhanden
                     linebags.Add(h, new PseudoLineBag());

                  int identh = 0; // Anzahl der Eckpunkte, die die gesuchte Höhe haben
                  if (ha == h)
                     identh++;
                  if (hb == h)
                     identh++;
                  if (hc == h)
                     identh++;

                  switch (identh) {
                     case 0:
                        pointlist_ptr = 0;
                        if ((ha <= h && h <= hb) || (ha >= h && h >= hb))
                           pointlist[pointlist_ptr++] = a.BetweenPoint(b, (h - ha) / (hb - ha));
                        if ((hb <= h && h <= hc) || (hb >= h && h >= hc))
                           pointlist[pointlist_ptr++] = b.BetweenPoint(c, (h - hb) / (hc - hb));
                        if ((hc <= h && h <= ha) || (hc >= h && h >= ha))
                           if (pointlist_ptr > 1)
                              pointlist_ptr++;
                           else
                              pointlist[pointlist_ptr++] = c.BetweenPoint(a, (h - hc) / (ha - hc));
                        if (pointlist_ptr != 2)
                           throw new Exception("Fehler im Algorithmus: Es müssen genau 2 Punkte gefunden werden!");

                        linebags[h].Add(pointlist[0], pointlist[1]);
                        break;

                     case 1:
                        // sicherstellen, dass A dieser Punkt ist
                        if (hb == h) {
                           RotateValues(true, ref ha, ref hb, ref hc, ref a, ref b, ref c);
                        } else if (hc == h) {
                           RotateValues(false, ref ha, ref hb, ref hc, ref a, ref b, ref c);
                        }
                        if (hmin < ha && ha < hmax)
                           linebags[h].Add(a, c.BetweenPoint(b, (h - hc) / (hb - hc)));
                        break;

                     case 2:
                        if (ha == hb)
                           linebags[h].Add(a, b);
                        else if (hb == hc)
                           linebags[h].Add(b, c);
                        else if (hc == ha)
                           linebags[h].Add(c, a);
                        break;

                        //case 3: schon aussortiert
                  }
               }
            }
         }
      }

      void RotateValues(bool left, ref double ha, ref double hb, ref double hc, ref PseudoPoint a, ref PseudoPoint b, ref PseudoPoint c) {
         double tmp = ha;
         PseudoPoint tmpp = a;
         if (left) {
            ha = hb;
            hb = hc;
            hc = tmp;
            a = b;
            b = c;
            c = tmpp;
         } else {
            ha = hc;
            hc = hb;
            hb = tmp;
            a = c;
            c = b;
            b = tmpp;
         }
      }

      /// <summary>
      /// schreibt die <see cref="PseudoPolyline"/> in die Streams
      /// </summary>
      /// <param name="swnodes"></param>
      /// <param name="swways"></param>
      /// <param name="ppl"></param>
      /// <param name="maxnodesperway"></param>
      /// <param name="ityp">Typ der Höhenlinie</param>
      /// <param name="height">Höhe der Höhenlinie</param>
      /// <param name="startidnodes">Start-ID für die Nodes</param>
      /// <param name="wayid">Way-ID</param>
      /// <param name="usertags">zusätzliche User-Tags</param>
      /// <returns>Anzahl der ways</returns>
      int WritePseudoPolyline(StreamWriter swnodes, StreamWriter swways,
                               PseudoPolyline ppl, uint maxnodesperway, IsohypsenTyp ityp, int height,
                               long startidnodes,
                               long wayid,
                               string usertags) {
         int ways = 1;
         long id = startidnodes;

         for (int i = 0; i < ppl.P.Count; i++, id++)
            swnodes.WriteLine("<node id='{0}' lat='{1}' lon='{2}'{3}/>",
                              id,
                              LatitudeAsText(ppl.P[i].Y), LongitudeAsText(ppl.P[i].X),
                              usertags);

         long n = startidnodes;
         while (n < id) {
            swways.WriteLine("<way id='{0}'{1}>", wayid, usertags);

            for (int i = 0; i < maxnodesperway && n < id; n++, i++) 
               swways.WriteLine("<nd ref='{0}' />", n);

            swways.WriteLine("<tag k='ele' v='{0}'/>", height);
            swways.WriteLine("<tag k='contour' v='elevation'/>");
            if (ityp != IsohypsenTyp.Nothing)
               swways.WriteLine("<tag k='contour_ext' v='{0}'/>",
                                    ityp == IsohypsenTyp.Major ? "elevation_major" :
                                    ityp == IsohypsenTyp.Medium ? "elevation_medium" : "elevation_minor");
            swways.WriteLine("</way>");

            wayid++;
            ways++;
            n--;
            if (n == id - 1)
               break;
         }

         return ways;
      }

      /// <summary>
      /// erzeugt zusätzliche Punkt- und Flächenobjekte in der OSM-Datei(i.A. nur zum Testen)
      /// </summary>
      /// <param name="sw"></param>
      /// <param name="swway"></param>
      /// <param name="ShowPoints">Koordinatenbereich für die Ausgabe von Nodes mit Höhenangaben</param>
      /// <param name="ShowAreas">Koordinatenbereich für die Ausgabe von Rechtecken mit Höhenangaben</param>
      /// <param name="sUserTags"></param>
      /// <param name="actid"></param>
      /// <returns></returns>
      long InsertOsmHeightDecoration(StreamWriter sw, StreamWriter swway, double[] ShowPoints, double[] ShowAreas, string sUserTags, long actid) {
         int pt = 0;

         if (ShowPoints != null &&
             ShowPoints.Length == 4) {
            Console.Error.WriteLine("schreibe Höhen der Datenpunkte im Bereich {0}°...{1}°, {2}°...{3}° als Punkte",
                                    ShowPoints[0], ShowPoints[0] + ShowPoints[2],
                                    ShowPoints[1], ShowPoints[1] + ShowPoints[3]);
            for (int x = 0; x < Width; x++)
               for (int y = 0; y < Height; y++) {
                  double lat = Latitude(y);
                  double lon = Longitude(x);
                  if (ShowPoints[0] <= lat && lat <= (ShowPoints[0] + ShowPoints[2]) &&
                      ShowPoints[1] <= lon && lon <= (ShowPoints[1] + ShowPoints[3])) {
                     sw.WriteLine("<node id='{0}' lat='{1}' lon='{2}'{3}>", actid++, LatitudeAsText(y), LongitudeAsText(x), sUserTags);
                     sw.WriteLine("<tag k='contour' v='elevationpoint'/>");
                     sw.WriteLine("<tag k='ele' v='{0}'/>", Get(x, y));
                     sw.WriteLine("</node>");
                     pt++;
                  }
               }
            Console.Error.WriteLine("{0} Punkte", pt);
         }

         if (ShowAreas != null &&
             ShowAreas.Length == 4) {
            Console.Error.WriteLine("schreibe Höhen der Datenpunkte im Bereich {0}°...{1}°, {2}°...{3}° als Flächen",
               ShowAreas[0], ShowAreas[0] + ShowAreas[2], ShowAreas[1], ShowAreas[1] + ShowAreas[3]);
            PointStore ps = new PointStore();
            List<long> waypoints = new List<long>();
            for (int x = 0; x < Width; x++)
               for (int y = 0; y < Height; y++) {
                  double lat = Latitude(y);
                  double lon = Longitude(x);
                  if (ShowAreas[0] <= lat && lat <= (ShowAreas[0] + ShowAreas[2]) &&
                      ShowAreas[1] <= lon && lon <= (ShowAreas[1] + ShowAreas[3])) {
                     waypoints.Add(ps.Add(new Point(x, y)));
                     waypoints.Add(ps.Add(new Point(x + 1, y)));
                     waypoints.Add(ps.Add(new Point(x + 1, y + 1)));
                     waypoints.Add(ps.Add(new Point(x, y + 1)));
                  }
               }

            double deltalat = (Latitude(1) - Latitude(0)) / 2;
            double deltalon = (Longitude(1) - Longitude(0)) / 2;
            foreach (long id in ps.AllIDs()) {
               Point p = ps.Point(id);
               sw.WriteLine("<node id='{0}' lat='{1}' lon='{2}'{3}/>",
                  actid - id,
                  (Latitude(p.Y) - deltalat).ToString(CultureInfo.InvariantCulture),
                  (Longitude(p.X) - deltalon).ToString(CultureInfo.InvariantCulture),
                  sUserTags);
            }
            for (int i = 0; i < waypoints.Count; i += 4) {
               swway.WriteLine("<way id='{0}'{1}>", actid - ps.NextID + i / 4, sUserTags);
               swway.WriteLine("<nd ref='{0}' />", actid - waypoints[i]);
               swway.WriteLine("<nd ref='{0}' />", actid - waypoints[i + 1]);
               swway.WriteLine("<nd ref='{0}' />", actid - waypoints[i + 2]);
               swway.WriteLine("<nd ref='{0}' />", actid - waypoints[i + 3]);
               swway.WriteLine("<nd ref='{0}' />", actid - waypoints[i]);
               Point p = ps.Point(waypoints[i]);
               swway.WriteLine("<tag k='contour' v='elevationarea'/>");
               swway.WriteLine("<tag k='ele' v='{0}'/>", Get(p.X, p.Y));
               swway.WriteLine("</way>");
               pt++;
            }
            actid += -ps.NextID + waypoints.Count / 4;

            Console.Error.WriteLine("{0} Rechtecke", pt);
            ShowProgressTime();
         }
         return actid;
      }


      #region Threadgroup

      abstract class Threadgroup {

         /// <summary>
         /// max. Anzahl von erlaubten Threads
         /// </summary>
         int max;
         /// <summary>
         /// Anzahl der akt. Threads
         /// </summary>
         int threadcount;
         /// <summary>
         /// Lock-Objekt für die interne Nutzung
         /// </summary>
         object locker;
         /// <summary>
         /// gesetzt, wenn kein Thread mehr läuft
         /// </summary>
         public ManualResetEvent NothingToDo { get; private set; }

         ManualResetEvent[] EndEvent;
         bool[] EndEventIsInUse;
         private int i;

         public Threadgroup(int max) {
            this.max = max;
            threadcount = 0;
            NothingToDo = new ManualResetEvent(false);
            EndEvent = new ManualResetEvent[max];
            EndEventIsInUse = new bool[max];
            for (int i = 0; i < EndEvent.Length; i++) {
               EndEvent[i] = new ManualResetEvent(false);
               EndEventIsInUse[i] = false;
            }
            locker = new object();
         }

         /// <summary>
         /// startet einen Thread (sofort, oder wenn wieder einer "frei wird")
         /// </summary>
         /// <param name="para"></param>
         public void Start(object para) {

            Monitor.Enter(locker);
            int actualthreadcount = threadcount;
            NothingToDo.Reset(); // auf jeden Fall
            Monitor.Exit(locker);

            // Index eines freien Threadplatzes ermitteln (ev. warten bis ein Threadplatz frei wird)
            int idx;
            if (actualthreadcount >= max) {                 // z.Z. kein freier Thread
               idx = WaitHandle.WaitAny(EndEvent);
               EndEvent[idx].Reset();
            } else {
               Monitor.Enter(locker);
               for (idx = 0; idx < EndEventIsInUse.Length; idx++)
                  if (!EndEventIsInUse[idx])
                     break;
            }

            if (!Monitor.IsEntered(locker))
               Monitor.Enter(locker);

            threadcount++;
            EndEventIsInUse[idx] = true;
            Thread t = new Thread(DoWorkFrame);       // Thread erzeugen ...
            t.Start(new object[] { idx, para });      // ... und starten

            Monitor.Exit(locker);
         }

         protected void DoWorkFrame(object para) {
            object[] data = para as object[];
            int freeidx = (int)data[0];
            DoWork(data[1]);

            Monitor.Enter(locker);

            threadcount--;
            EndEventIsInUse[freeidx] = false;
            EndEvent[freeidx].Set();
            if (threadcount == 0)
               NothingToDo.Set(); // das war der letzte Thread

            Monitor.Exit(locker);
         }

         protected virtual void DoWork(object para) { }

      }

      ///// <summary>
      ///// Verwaltung einer Threadgruppe
      ///// </summary>
      //abstract class Threadgroup2 {
      //   /// <summary>
      //   /// max. Anzahl von erlaubten Threads
      //   /// </summary>
      //   int max;
      //   /// <summary>
      //   /// Anzahl der akt. Threads
      //   /// </summary>
      //   int threadcount;
      //   /// <summary>
      //   /// Wartet ein Thread auf seinen Start?
      //   /// </summary>
      //   bool waiting;
      //   /// <summary>
      //   /// Lock-Objekt für die interne Nutzung
      //   /// </summary>
      //   object locker;

      //   /// <summary>
      //   /// wird gesetzt wenn keine Threads mehr laufen
      //   /// </summary>
      //   public ManualResetEvent Empty { get; private set; }
      //   /// <summary>
      //   /// wird immer gesetzt, wenn ein Thread beendet wird
      //   /// </summary>
      //   private ManualResetEvent EndThread;

      //   public Threadgroup2(int max) {
      //      this.max = max;
      //      Empty = new ManualResetEvent(false);
      //      EndThread = new ManualResetEvent(false);
      //      threadcount = 0;
      //      locker = new object();
      //      waiting = false;
      //   }

      //   /// <summary>
      //   /// startet einen neuen Thread bzw. wartet notfalls solange bis ein neuer gestartet werden kann;
      //   /// Funktion wird nur vom Haupt-Thread aufgerufen, d.h. wenn sie im Wartezustand ist, wartet auch der Hauptthread
      //   /// </summary>
      //   /// <param name="para">Parameter für die Threadfunktion</param>
      //   /// 
      //   public void Start(object para) {
      //      Monitor.Enter(locker);
      //      //Console.Error.WriteLine("Start (1): threadcount={0}", threadcount);
      //      if (threadcount >= max) {                             // kein Thread mehr möglich ...
      //         waiting = true;
      //         EndThread.Reset();                                 //    (sichern, dass das Event nicht signalisiert ist, um selbst darauf warten zu können)
      //         Monitor.Exit(locker);
      //         //Console.Error.WriteLine("Warte ...");
      //         EndThread.WaitOne();                               // ... auf Ende eines Threads warten 
      //      } else
      //         Monitor.Exit(locker);
      //      Monitor.Enter(locker);
      //      waiting = false;
      //      threadcount++;                                           // Thread wird sicherheitshalber schon gezählt, obwohl er noch nicht gestartet ist
      //      Empty.Reset();                                           // sichern, dass das Event nicht signalisiert ist
      //                                                               //Console.Error.WriteLine("Start (2): threadcount={0}", threadcount);
      //      Monitor.Exit(locker);
      //      Thread t = new Thread(DoWorkFrame);                      // Thread erzeugen ...
      //      t.Start(para);                                           // ... und starten
      //   }

      //   protected void DoWorkFrame(object para) {
      //      DoWork(para);                                            // eigentliche Arbeit ausführen

      //      Monitor.Enter(locker);
      //      threadcount--;
      //      EndThread.Set();                                         // Signalisierung, dass ein Auftrag beendet wurde
      //                                                               //Console.Error.WriteLine("DoWorkFrame (1): threadcount={0}", threadcount);
      //      if (threadcount == 0 &&                                  // akt. kein Auftrag in Arbeit und
      //          !waiting)                                            // kein wartetender Auftrag mehr
      //         Empty.Set();                                          // Signalisierung, dass kein Auftrag mehr bearbeitet werden muss
      //      Monitor.Exit(locker);
      //   }

      //   protected virtual void DoWork(object para) { }
      //}

      /// <summary>
      /// Threadgruppe für die Verkettung
      /// </summary>
      class BuildPseudoPolylinesThreadgroup : Threadgroup {

         public delegate void Info(string txt);
         public event Info InfoEvent;

         public BuildPseudoPolylinesThreadgroup(int max)
            : base(max) { }

         protected override void DoWork(object para) {
            if (para is object[]) {
               object[] args = para as object[];
               if (args.Length == 3) {
                  if (args[0] is int &&
                      args[1] is PseudoLineBag &&
                      args[2] is PseudoPolylineBag) {
                     int height = (int)args[0];
                     PseudoLineBag plbag = args[1] as PseudoLineBag;
                     PseudoPolylineBag pplbag = args[2] as PseudoPolylineBag;
                     int lines = plbag.Count();
                     pplbag.BuildPseudoPolylines(plbag);
                     InfoEvent(string.Format("Höhe {0}: {1} Strecken komprimiert -> {2} Polylinie, davon {3} geschlossen",
                                             height, lines, pplbag.Polylines.Count, pplbag.ClosedPolylineCount()));
                  }
               }
            }
         }

      }

      /// <summary>
      /// Zwischenmeldung damit der Anwender nicht nervös wird
      /// </summary>
      void tg_InfoEvent(string txt) {
         lock (consolewritelocker) {
            Console.WriteLine(txt);
         }
      }

      #endregion

      #region Hilfsklassen für Decoration

      /// <summary>
      /// eine einfache Punktklasse
      /// </summary>
      class Point : IComparable {
         public short X { get; private set; }
         public short Y { get; private set; }

         public Point(int x, int y) {
            X = (short)x;
            Y = (short)y;
         }
         public Point()
            : this(0, 0) { }
         public Point(Point p)
            : this(p.X, p.Y) { }

         public double DistanceSquare(Point p) {
            return (p.X - X) * (p.X - X) + (p.Y - Y) * (p.Y - Y);
         }

         public static Point operator -(Point p1, Point p2) {
            return new Point(p2.X - p1.X, p2.Y - p1.Y);
         }

         public static bool operator ==(Point p1, Point p2) {
            return p1.X == p2.X && p1.Y == p2.Y;
         }

         public static bool operator !=(Point p1, Point p2) {
            return !(p1 == p2);
         }

         public override bool Equals(Object obj) {
            return obj is Point && this == (Point)obj;
         }

         public override int GetHashCode() {
            return X ^ Y;
         }

         /// <summary>
         /// Hilfsfunktion zum Vergleichen für die Sortierung
         /// </summary>
         /// <param name="obj"></param>
         /// <returns></returns>
         public int CompareTo(object obj) {
            if (obj == null) return 0;
            if (obj is Point) {
               Point pt = (Point)obj;
               // this > vd --> 1
               if (X == pt.X) {
                  if (Y == pt.Y) return 0;
                  return Y > pt.Y ? 1 : -1;
               } else
                  return X > pt.X ? 1 : -1;
            }
            return 0;
         }


         public override string ToString() {
            return string.Format("[X({0}), Y({1})]", X, Y);
         }

      }

      /// <summary>
      /// Sammlung aller Punkte mit ihren Pseudo-ID's für einen IsohypseLevel
      /// </summary>
      class PointStore {

         /// <summary>
         /// Liste aller Punkte mit ihren Pseudo-ID's für diesen IsohypseLevel
         /// </summary>
         SortedList<long, Point> Points;
         /// <summary>
         /// Liste aller Pseudo-ID's mit ihrem Punkt für diesen IsohypseLevel
         /// </summary>
         public SortedList<Point, long> PointIDs;

         /// <summary>
         /// liefert die nächste verwendbare ID (-1 ... !)
         /// </summary>
         public long NextID { get; private set; }

         public PointStore() {
            Points = new SortedList<long, Point>();
            PointIDs = new SortedList<Point, long>();
            NextID = -1;
         }

         /// <summary>
         /// nimmt (nur wennn noch nicht vorhanden) den Punkt auf und liefert die ID
         /// </summary>
         /// <param name="pt"></param>
         /// <returns></returns>
         public long Add(Point pt) {
            if (PointIDs.ContainsKey(pt))
               return PointIDs[pt];
            Points.Add(NextID, pt);
            PointIDs.Add(pt, NextID);
            return NextID--;
         }

         /// <summary>
         /// liefert alle vorhandenen ID's
         /// </summary>
         /// <returns></returns>
         public IList<long> AllIDs() {
            return Points.Keys;
         }

         /// <summary>
         /// liefert alle vorhandenen Punkte
         /// </summary>
         /// <returns></returns>
         public IList<Point> AllPoints() {
            return PointIDs.Keys;
         }

         /// <summary>
         /// liefert die ID zum Punkt (0 bedeutet Fehler)
         /// </summary>
         /// <param name="pt"></param>
         /// <returns></returns>
         public long ID(Point pt) {
            return PointIDs.ContainsKey(pt) ? PointIDs[pt] : 0;
         }

         /// <summary>
         /// liefert den Punkt zur ID ((-1, -1) bedeutet Fehler)
         /// </summary>
         /// <param name="id"></param>
         /// <returns></returns>
         public Point Point(long id) {
            return Points.ContainsKey(id) ? Points[id] : new Point(-1, -1);
         }

      }

      #endregion


   }
}
