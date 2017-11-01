/*
Copyright (C) 2011 Frank Stinner

This program is free software; you can redistribute it and/or modify it 
under the terms of the GNU General Public License as published by the 
Free Software Foundation; either version 3 of the License, or (at your 
option) any later version. 

This program is distributed in the hope that it will be useful, but 
WITHOUT ANY WARRANTY; without even the implied warranty of 
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General 
Public License for more details. 

You should have received a copy of the GNU General Public License along 
with this program; if not, see <http://www.gnu.org/licenses/>. 


Dieses Programm ist freie Software. Sie können es unter den Bedingungen 
der GNU General Public License, wie von der Free Software Foundation 
veröffentlicht, weitergeben und/oder modifizieren, entweder gemäß 
Version 3 der Lizenz oder (nach Ihrer Option) jeder späteren Version. 

Die Veröffentlichung dieses Programms erfolgt in der Hoffnung, daß es 
Ihnen von Nutzen sein wird, aber OHNE IRGENDEINE GARANTIE, sogar ohne 
die implizite Garantie der MARKTREIFE oder der VERWENDBARKEIT FÜR EINEN 
BESTIMMTEN ZWECK. Details finden Sie in der GNU General Public License. 

Sie sollten ein Exemplar der GNU General Public License zusammen mit 
diesem Programm erhalten haben. Falls nicht, siehe 
<http://www.gnu.org/licenses/>. 
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Drawing;

namespace Hgt2Osm {

   /// <summary>
   /// Optionen und Argumente werden zweckmäßigerweise in eine (programmabhängige) Klasse gekapselt.
   /// Erzeugen des Objektes und Evaluate() sollten in einem try-catch-Block erfolgen.
   /// </summary>
   public class Options {

      // alle Optionen sind 'read-only'

      /// <summary>
      /// Latitude (südliche Breite negativ)
      /// </summary>
      public int[] Latitude { get; private set; }
      /// <summary>
      /// Longitude (westliche Länge negativ)
      /// </summary>
      public int[] Longitude { get; private set; }
      /// <summary>
      /// Pfad zum HGT-Verzeichnis
      /// </summary>
      public string HGTPath { get; private set; }
      /// <summary>
      /// erste verwendete OSM-ID (wenn negativ, dann aus den Koordinaten abgeleitet)
      /// </summary>
      public long FirstID { get; private set; }
      /// <summary>
      /// Name der OSM-Datei/en
      /// </summary>
      public string[] Outfile { get; private set; }
      /// <summary>
      /// Name der Gesamt-OSM-Datei
      /// </summary>
      public string Mergefile { get; private set; }
      /// <summary>
      /// vorhandene Ausgabedateien überschreiben
      /// </summary>
      public bool OutputOverwrite { get; private set; }
      /// <summary>
      /// max. Anzahl der Threads
      /// </summary>
      public uint MaxThreads { get; private set; }

      /// <summary>
      /// Abstand zweier Höhenlinien
      /// </summary>
      public uint MinorDistance { get; private set; }
      /// <summary>
      /// Anzahl der Höhenlinien, nach denen jeweils eine mittlere Höhenlinien kommt
      /// </summary>
      public uint MediumFactor { get; private set; }
      /// <summary>
      /// Anzahl der mittlere Höhenlinien, nach denen jeweils eine Haupt-Höhenlinie kommt
      /// </summary>
      public uint MajorFactor { get; private set; }

      /// <summary>
      /// min. Punktanzahl je Linie
      /// </summary>
      public uint MinVerticePoints { get; private set; }
      /// <summary>
      /// min. Boundingbox der Linie in Grad
      /// </summary>
      public double MinBoundingbox { get; private set; }
      /// <summary>
      /// Parameter für Douglas-Peucker-Algorithmus
      /// </summary>
      public double DouglasPeucker { get; private set; }

      /// <summary>
      /// max. Anzahl von Nodes je Way
      /// </summary>
      public uint MaxNodesPerWay { get; private set; }
      /// <summary>
      /// Typ der Höhenlinie ausgeben
      /// </summary>
      public bool WriteElevationType { get; private set; }

      // Verwendung der OSM-Attribute
      /// <summary>
      /// wenn true, wird das Bound-Tag eingefügt
      /// </summary>
      public bool OSMBound { get; private set; }
      /// <summary>
      /// wenn true, wird das Bounds-Tag eingefügt
      /// </summary>
      public bool OSMBounds { get; private set; }
      /// <summary>
      /// wenn true, wird das Visible-Tag eingefügt
      /// </summary>
      public bool OSMVisible { get; private set; }
      /// <summary>
      /// wenn nicht leer, wird das User-Tag eingefügt
      /// </summary>
      public string OSMUser { get; private set; }
      /// <summary>
      /// wenn größer 0, wird das Version-Tag eingefügt
      /// </summary>
      public uint OSMVersion { get; private set; }
      /// <summary>
      /// wenn true, wird das Timestamp-Tag eingefügt
      /// </summary>
      public bool OSMTimestamp { get; private set; }

      /// <summary>
      /// wenn true, wird nur eine georef. PNG-Datei aus den HGT-Daten erzeugt
      /// </summary>
      public bool OnlyGeoPng { get; private set; }
      /// <summary>
      /// Dummy-Farbe im georeferenzierten PNG-Bitmap
      /// </summary>
      public Color DummyGeoColor { get; private set; }
      /// <summary>
      /// Liste der Höhen und Farben im georeferenzierten PNG-Bitmap
      /// </summary>
      public SortedDictionary<int, Color> GeoColor { get; private set; }

      /// <summary>
      /// wenn true, wird nur eine ArcInfoASCIIGrid-Datei der interpolierten Werte erzeugt
      /// </summary>
      public bool OnlyArcInfoASCIIGrid { get; private set; }

      /// <summary>
      /// generelle Höhenkorrektur in Metern
      /// </summary>
      public double FakeDistance { get; private set; }
      /// <summary>
      /// wenn größer 0 wird ein Bitmap mit den Strecken mit der entsprechenden Größe erzeugt
      /// </summary>
      public uint LineBitmapWidth { get; private set; }
      /// <summary>
      /// wenn größer 0 wird ein Bitmap mit den Polylinien mit der entsprechenden Größe erzeugt
      /// </summary>
      public uint PolylineBitmapWidth { get; private set; }
      /// <summary>
      /// wenn true, werden Höhendaten als Text ausgegeben
      /// </summary>
      public bool Textdata { get; private set; }

      public double[] ShowPoints { get; private set; }
      public double[] ShowAreas { get; private set; }

      /// <summary>
      /// Programm-Parameter
      /// </summary>
      public string[] Parameter { get; private set; }


      FsoftUtils.CmdlineOptions cmd;

      /// <summary>
      /// Optionswerte (Reihenfolge ist für die Ausgabe der Hilfe wichtig!)
      /// </summary>
      enum MyOptions {
         Lat, Lon, HGTPath, FirstID,
         Outfile, Mergefile, OutputOverwrite, MaxThreads,
         MinVerticePoints, MinBoundingbox, DouglasPeucker,

         MinorDistance, MediumFactor, MajorFactor,

         MaxNodesPerWay, WriteElevationType,

         FakeDistance,

         OSMBound, OSMBounds, OSMVisible, OSMUser, OSMVersion, OSMTimestamp,

         OnlyGeoPng, DummyGeoColor, GeoColor,
         OnlyArcInfoASCIIGrid,
         LineBitmapWidth, PolylineBitmapWidth, Textdata,
         ShowPoints, ShowAreas,

         MyHelp
      }

      public Options() {
         Init();
         cmd = new FsoftUtils.CmdlineOptions();
         // Definition der Optionen
         cmd.DefineOption((int)MyOptions.Lat, "Lat", "", "Latitude (südliche Breite negativ; mehrfach verwendbar)", FsoftUtils.CmdlineOptions.OptionArgumentType.Integer, int.MaxValue);
         cmd.DefineOption((int)MyOptions.Lon, "Lon", "", "Longitude (westliche Länge negativ; mehrfach verwendbar)", FsoftUtils.CmdlineOptions.OptionArgumentType.Integer, int.MaxValue);
         cmd.DefineOption((int)MyOptions.HGTPath, "HgtPath", "g", "Pfad zum HGT-Verzeichnis", FsoftUtils.CmdlineOptions.OptionArgumentType.String);
         cmd.DefineOption((int)MyOptions.FirstID, "FirstID", "f", "erste verwendete OSM-ID (wenn negativ, dann aus den Koordinaten abgeleitet)" + System.Environment.NewLine +
                                                                   "(Standard -1)", FsoftUtils.CmdlineOptions.OptionArgumentType.Long);
         cmd.DefineOption((int)MyOptions.Outfile, "Outfile", "o", "Name der OSM-Datei (mehrfach verwendbar) (Standard leer)", FsoftUtils.CmdlineOptions.OptionArgumentType.String, int.MaxValue);
         cmd.DefineOption((int)MyOptions.Mergefile, "Mergefile", "m", "Name der Gesamt-OSM-Datei", FsoftUtils.CmdlineOptions.OptionArgumentType.String);
         cmd.DefineOption((int)MyOptions.OutputOverwrite, "OutputOverwrite", "r", "vorhandene Ausgabedateien überschreiben", FsoftUtils.CmdlineOptions.OptionArgumentType.Boolean);
         cmd.DefineOption((int)MyOptions.MaxThreads, "MaxThreads", "t", "Anzahl der Threads (Standard 0, d.h. automatisch entsprechend der Prozessorkerne)", FsoftUtils.CmdlineOptions.OptionArgumentType.PositivInteger);
         cmd.DefineOption((int)MyOptions.MinVerticePoints, "MinVerticePoints", "", "min. Punkt-Anzahl einer Linie (Standard 3)", FsoftUtils.CmdlineOptions.OptionArgumentType.UnsignedInteger);
         cmd.DefineOption((int)MyOptions.MinBoundingbox, "MinBoundingbox", "", "min. Größe der Boundigbox einer Linie in Grad (Standard 0.0005)", FsoftUtils.CmdlineOptions.OptionArgumentType.Double);
         cmd.DefineOption((int)MyOptions.DouglasPeucker, "DouglasPeucker", "d", "Parameter für Douglas-Peucker-Algorithmus (Standard 0.1; Abschaltung mit 0)" + System.Environment.NewLine +
                                                                                "Punkte deren Abstand zur Teilstrecke kleiner ist, werden entfernt", FsoftUtils.CmdlineOptions.OptionArgumentType.Double);

         cmd.DefineOption((int)MyOptions.MinorDistance, "MinorDistance", "", "Abstand zweier Höhenlinien (Standard 20)", FsoftUtils.CmdlineOptions.OptionArgumentType.UnsignedInteger);
         cmd.DefineOption((int)MyOptions.MediumFactor, "MediumFactor", "", "Anzahl der Höhenlinien, nach denen jeweils eine\nmittlere Höhenlinien kommt (Standard 5)", FsoftUtils.CmdlineOptions.OptionArgumentType.PositivInteger);
         cmd.DefineOption((int)MyOptions.MajorFactor, "MajorFactor", "", "Anzahl der mittlere Höhenlinien, nach denen\njeweils eine Haupt-Höhenlinien kommt (Standard 5)", FsoftUtils.CmdlineOptions.OptionArgumentType.UnsignedInteger);

         cmd.DefineOption((int)MyOptions.MaxNodesPerWay, "MaxNodesPerWay", "", "max. Anzahl von Nodes je Way (Standard 500)", FsoftUtils.CmdlineOptions.OptionArgumentType.UnsignedInteger);
         cmd.DefineOption((int)MyOptions.WriteElevationType, "WriteElevationType", "", "Typ der Höhenlinie als Tag 'contour_ext' ausgeben (Standard true)", FsoftUtils.CmdlineOptions.OptionArgumentType.Boolean);

         cmd.DefineOption((int)MyOptions.FakeDistance, "FakeDistance", "", "generelle Höhenkorrektur in Metern (Standard -0.5)", FsoftUtils.CmdlineOptions.OptionArgumentType.Double);

         cmd.DefineOption((int)MyOptions.OSMBound, "OSMBound", "", "fügt das Bound-Tag ein", FsoftUtils.CmdlineOptions.OptionArgumentType.Nothing);
         cmd.DefineOption((int)MyOptions.OSMBounds, "OSMBounds", "", "fügt das Bounds-Tag ein (Standard)", FsoftUtils.CmdlineOptions.OptionArgumentType.Nothing);
         cmd.DefineOption((int)MyOptions.OSMVisible, "OSMVisible", "", "fügt das Visible-Tag ein", FsoftUtils.CmdlineOptions.OptionArgumentType.Nothing);
         cmd.DefineOption((int)MyOptions.OSMUser, "OSMUser", "", "fügt das User-Tag ein", FsoftUtils.CmdlineOptions.OptionArgumentType.String);
         cmd.DefineOption((int)MyOptions.OSMVersion, "OSMVersion", "", "wenn > 0, wird das Version-Tag eingefügt (Standard 1)", FsoftUtils.CmdlineOptions.OptionArgumentType.UnsignedInteger);
         cmd.DefineOption((int)MyOptions.OSMTimestamp, "OSMTimestamp", "", "fügt das Timestamp-Tag ein (Standard)", FsoftUtils.CmdlineOptions.OptionArgumentType.Boolean);

         cmd.DefineOption((int)MyOptions.OnlyGeoPng, "OnlyGeoPng", "", "erzeugt nur ein georeferenziertes PNG-Bitmap", FsoftUtils.CmdlineOptions.OptionArgumentType.Nothing);
         cmd.DefineOption((int)MyOptions.DummyGeoColor, "DummyGeoColor", "", "Dummy-Farbe im georeferenzierten PNG-Bitmap", FsoftUtils.CmdlineOptions.OptionArgumentType.String);
         cmd.DefineOption((int)MyOptions.GeoColor, "GeoColor", "", "Höhe und Farbe im georeferenzierten PNG-Bitmap", FsoftUtils.CmdlineOptions.OptionArgumentType.String, int.MaxValue);

         cmd.DefineOption((int)MyOptions.OnlyArcInfoASCIIGrid, "OnlyArcInfoASCIIGrid", "", "erzeugt nur eine ArcInfoASCIIGrid-Datei der interpolierten Werte", FsoftUtils.CmdlineOptions.OptionArgumentType.Nothing);

         cmd.DefineOption((int)MyOptions.LineBitmapWidth, "LineBitmapWidth", "", "wenn > 0, wird ein Bitmap der berechneten Strecken mit dieser Breite in" + System.Environment.NewLine +
                                                                                 "Pixeln erzeugt (Standard 0)", FsoftUtils.CmdlineOptions.OptionArgumentType.UnsignedInteger);
         cmd.DefineOption((int)MyOptions.PolylineBitmapWidth, "PolylineBitmapWidth", "", "wenn > 0, wird ein Bitmap der berechneten Polylinien mit dieser Breite in" + System.Environment.NewLine +
                                                                                         "Pixeln erzeugt (Standard 0)", FsoftUtils.CmdlineOptions.OptionArgumentType.UnsignedInteger);
         cmd.DefineOption((int)MyOptions.Textdata, "Textdata", "", "gibt die Höhendaten als Textdatei aus", FsoftUtils.CmdlineOptions.OptionArgumentType.Nothing);

         cmd.DefineOption((int)MyOptions.ShowPoints, "ShowPoints", "", "Datenpunkte der Höhen im Bereich (lat, lon, Höhe, Breite in Grad)\nals Nodes in die OSM-Datei einfügen (Tag 'contour'='elevationpoint' und Tag 'ele')", FsoftUtils.CmdlineOptions.OptionArgumentType.String);
         cmd.DefineOption((int)MyOptions.ShowAreas, "ShowAreas", "", "Datenpunkte der Höhen im Bereich (lat, lon, Höhe, Breite in Grad)\nals Ways in die OSM-Datei einfügen (Tag 'contour'='elevationarea' und Tag 'ele')", FsoftUtils.CmdlineOptions.OptionArgumentType.String);



         cmd.DefineOption((int)MyOptions.MyHelp, "help", "?", "Hilfe", FsoftUtils.CmdlineOptions.OptionArgumentType.Nothing);
      }

      /// <summary>
      /// Standardwerte setzen
      /// </summary>
      void Init() {
         Latitude = new int[] { };
         Longitude = new int[] { };
         HGTPath = ".\\";
         FirstID = -1; // 1000000000;
         Outfile = new string[] { "" };
         Mergefile = "";
         OutputOverwrite = false;
         MaxThreads = 0;
         MinVerticePoints = 3;
         MinBoundingbox = 0.0005;
         DouglasPeucker = .04;

         MinorDistance = 20;
         MediumFactor = 5;
         MajorFactor = 5;

         MaxNodesPerWay = 500;
         WriteElevationType = true;

         OSMBound = false;
         OSMBounds = true;
         OSMVisible = false;
         OSMUser = "";
         OSMVersion = 1;
         OSMTimestamp = true;

         OnlyGeoPng = false;
         DummyGeoColor = Color.Black;
         GeoColor = new SortedDictionary<int, Color>();

         OnlyArcInfoASCIIGrid = false;

         LineBitmapWidth = 0;
         PolylineBitmapWidth = 0;
         FakeDistance = -0.5;
         Textdata = false;
         ShowPoints = null;
         ShowAreas = null;

      }

      /// <summary>
      /// Auswertung der Optionen
      /// </summary>
      /// <param name="args"></param>
      public void Evaluate(string[] args) {
         if (args == null) return;
         List<string> Outfile_Tmp = new List<string>();
         List<int> Lat_Tmp = new List<int>();
         List<int> Lon_Tmp = new List<int>();
         try {
            cmd.Parse(args);

            foreach (MyOptions opt in Enum.GetValues(typeof(MyOptions))) {    // jede denkbare Option testen
               int optcount = cmd.OptionAssignment((int)opt);                 // Wie oft wurde diese Option verwendet?
               if (optcount > 0)
                  switch (opt) {
                     case MyOptions.Lat:
                        for (int i = 0; i < optcount; i++)
                           Lat_Tmp.Add(cmd.IntegerValue((int)opt, i));
                        break;

                     case MyOptions.Lon:
                        for (int i = 0; i < optcount; i++)
                           Lon_Tmp.Add(cmd.IntegerValue((int)opt, i));
                        break;

                     case MyOptions.HGTPath:
                        HGTPath = cmd.StringValue((int)opt);
                        break;

                     case MyOptions.FirstID:
                        FirstID = cmd.LongValue((int)opt);
                        break;

                     case MyOptions.Outfile:
                        for (int i = 0; i < optcount; i++)
                           Outfile_Tmp.Add(cmd.StringValue((int)opt, i));
                        break;

                     case MyOptions.Mergefile:
                        Mergefile = cmd.StringValue((int)opt);
                        break;

                     case MyOptions.OutputOverwrite:
                        OutputOverwrite = cmd.BooleanValue((int)opt);
                        break;

                     case MyOptions.MaxThreads:
                        MaxThreads = cmd.PositivIntegerValue((int)opt);
                        break;

                     case MyOptions.MinVerticePoints:
                        MinVerticePoints = cmd.UnsignedIntegerValue((int)opt);
                        break;

                     case MyOptions.MinBoundingbox:
                        MinBoundingbox = cmd.DoubleValue((int)opt);
                        break;

                     case MyOptions.DouglasPeucker:
                        DouglasPeucker = cmd.DoubleValue((int)opt);
                        break;

                     case MyOptions.MinorDistance:
                        MinorDistance = cmd.UnsignedIntegerValue((int)opt);
                        break;

                     case MyOptions.MediumFactor:
                        MediumFactor = cmd.UnsignedIntegerValue((int)opt);
                        break;

                     case MyOptions.MajorFactor:
                        MajorFactor = cmd.UnsignedIntegerValue((int)opt);
                        break;

                     case MyOptions.MaxNodesPerWay:
                        MaxNodesPerWay = cmd.UnsignedIntegerValue((int)opt);
                        break;

                     case MyOptions.WriteElevationType:
                        WriteElevationType = cmd.BooleanValue((int)opt);
                        break;

                     case MyOptions.FakeDistance:
                        FakeDistance = cmd.DoubleValue((int)opt);
                        break;

                     case MyOptions.OSMBound:
                        OSMBound = !OSMBound;
                        break;

                     case MyOptions.OSMBounds:
                        OSMBounds = !OSMBounds;
                        break;

                     case MyOptions.OSMVisible:
                        OSMVisible = !OSMVisible;
                        break;

                     case MyOptions.OSMUser:
                        OSMUser = cmd.StringValue((int)opt);
                        break;

                     case MyOptions.OSMVersion:
                        OSMVersion = cmd.UnsignedIntegerValue((int)opt);
                        break;

                     case MyOptions.OSMTimestamp:
                        OSMTimestamp = cmd.BooleanValue((int)opt);
                        break;

                     case MyOptions.OnlyGeoPng:
                        OnlyGeoPng = !OnlyGeoPng;
                        break;

                     case MyOptions.DummyGeoColor:
                        DummyGeoColor = Color4String(cmd.StringValue((int)opt));
                        break;

                     case MyOptions.GeoColor: {
                           for (int i = 0; i < optcount; i++) {
                              int height;
                              Color col = ColorAndHeight4String(cmd.StringValue((int)opt, i), out height);
                              if (GeoColor.ContainsKey(height))
                                 throw new Exception(string.Format("Fehler beim Argument für {0}: Für die Höhe {1} ist schon eine Farbe definiert.", cmd.OptionName((int)opt), height));
                              GeoColor.Add(height, col);
                           }
                        }
                        break;

                     case MyOptions.OnlyArcInfoASCIIGrid:
                        OnlyArcInfoASCIIGrid = !OnlyArcInfoASCIIGrid;
                        break;

                     case MyOptions.LineBitmapWidth:
                        LineBitmapWidth = cmd.UnsignedIntegerValue((int)opt);
                        break;

                     case MyOptions.PolylineBitmapWidth:
                        PolylineBitmapWidth = cmd.UnsignedIntegerValue((int)opt);
                        break;

                     case MyOptions.Textdata:
                        Textdata = !Textdata;
                        break;


                     case MyOptions.ShowPoints: {
                           string[] tmp = cmd.StringValue((int)opt).Split(new char[] { '/' });
                           double[] area = new double[tmp.Length];
                           for (int i = 0; i < area.Length; i++)
                              area[i] = Convert.ToDouble(tmp[i], CultureInfo.InvariantCulture);
                           if (area.Length != 4 ||
                               area[0] < -90 || 90 < area[0] ||
                               area[1] < -180 || 180 < area[1] ||
                               area[2] == 0 || area[3] == 0)
                              throw new Exception(string.Format("Fehler beim Argument für {0}.",
                                 cmd.OptionName((int)opt)));
                           ShowPoints = new double[] { area[0], area[1], area[2], area[3] };
                        }
                        break;

                     case MyOptions.ShowAreas: {
                           string[] tmp = cmd.StringValue((int)opt).Split(new char[] { '/' });
                           double[] area = new double[tmp.Length];
                           for (int i = 0; i < area.Length; i++)
                              area[i] = Convert.ToDouble(tmp[i], CultureInfo.InvariantCulture);
                           if (area.Length != 4 ||
                               area[0] < -90 || 90 < area[0] ||
                               area[1] < -180 || 180 < area[1] ||
                               area[2] == 0 || area[3] == 0)
                              throw new Exception(string.Format("Fehler beim Argument für {0}.",
                                 cmd.OptionName((int)opt)));
                           ShowAreas = new double[] { area[0], area[1], area[2], area[3] };
                        }
                        break;


                     case MyOptions.MyHelp:
                        ShowHelp();
                        break;
                  }
            }

            Parameter = new string[cmd.Parameters.Count];
            cmd.Parameters.CopyTo(Parameter);

            if (cmd.Parameters.Count > 0)
               throw new Exception("Es sind keine Argumente sondern nur Optionen erlaubt.");

            if (Lat_Tmp.Count != Lon_Tmp.Count)
               throw new Exception("Latitude und Longitude müssen gleich oft verwendet werden.");

            //if (Lat_Tmp.Count != Outfile_Tmp.Count)
            //   throw new Exception("Latitude/Longitude und Outfile müssen gleich oft verwendet werden.");

            Outfile = new string[Outfile_Tmp.Count];
            Outfile_Tmp.CopyTo(Outfile);

            if (Lat_Tmp.Count > 0) {
               Latitude = new int[Lat_Tmp.Count];
               Lat_Tmp.CopyTo(Latitude);
            }

            if (Lon_Tmp.Count > 0) {
               Longitude = new int[Lon_Tmp.Count];
               Lon_Tmp.CopyTo(Longitude);
            }

            if (OSMBound && OSMBounds)
               throw new Exception("OSMBound und OSMBounds können nicht gleichzeitig verwendet werden.");

         } catch (Exception ex) {
            Console.Error.WriteLine(ex.Message);
            ShowHelp();
            throw new Exception("Fehler beim Ermitteln oder Anwenden der Programmoptionen.");
         }
      }


      Color Color4String(string txt) {
         string[] coltxt = txt.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
         if (!(coltxt.Length == 3 ||
               coltxt.Length == 4))
            throw new Exception(string.Format("Fehler bei Farbdefinition '{0}'.", txt));

         try {
            return coltxt.Length == 3 ?
                   Color.FromArgb(Convert.ToInt32(coltxt[0]) & 0xFF,
                                  Convert.ToInt32(coltxt[1]) & 0xFF,
                                  Convert.ToInt32(coltxt[2]) & 0xFF) :
                   Color.FromArgb(Convert.ToInt32(coltxt[0]) & 0xFF,
                                  Convert.ToInt32(coltxt[1]) & 0xFF,
                                  Convert.ToInt32(coltxt[2]) & 0xFF,
                                  Convert.ToInt32(coltxt[3]) & 0xFF);
         } catch (Exception ex) {
            throw new Exception(string.Format("Fehler bei Farbdefinition '{0}': {1}.", txt, ex.Message));
         }
      }

      Color ColorAndHeight4String(string txt, out int height) {
         height = int.MinValue;
         string[] coltxt = txt.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
         if (!(coltxt.Length == 4 ||
               coltxt.Length == 5))
            throw new Exception(string.Format("Fehler bei Farbdefinition '{0}'.", txt));

         try {
            height = Convert.ToInt16(coltxt[0]);
            return coltxt.Length == 4 ?
                   Color.FromArgb(Convert.ToInt32(coltxt[1]) & 0xFF,
                                  Convert.ToInt32(coltxt[2]) & 0xFF,
                                  Convert.ToInt32(coltxt[3]) & 0xFF) :
                   Color.FromArgb(Convert.ToInt32(coltxt[1]) & 0xFF,
                                  Convert.ToInt32(coltxt[2]) & 0xFF,
                                  Convert.ToInt32(coltxt[3]) & 0xFF,
                                  Convert.ToInt32(coltxt[4]) & 0xFF);
         } catch (Exception ex) {
            throw new Exception(string.Format("Fehler bei Farbdefinition '{0}': {1}.", txt, ex.Message));
         }
      }

      /// <summary>
      /// Hilfetext für Optionen ausgeben
      /// </summary>
      /// <param name="cmd"></param>
      public void ShowHelp() {
         List<string> help = cmd.GetHelpText();
         for (int i = 0; i < help.Count; i++) Console.Error.WriteLine(help[i]);
         Console.Error.WriteLine();
         //Console.Error.WriteLine("Zusatzinfos:");


         Console.Error.WriteLine("Für '--' darf auch '/' stehen und für '=' Leerzeichen oder ':'.");
         Console.Error.WriteLine();

         // ...

      }


   }
}
