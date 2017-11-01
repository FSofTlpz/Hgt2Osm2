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
using System.Reflection;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Text;
using System.Drawing;
using System.Collections.Generic;

namespace Hgt2Osm {
   class Program {

      static void Main(string[] args) {

         try {
            Options opt = new Options();
            opt.Evaluate(args);

            Console.Error.WriteLine(Title());
            Console.Error.WriteLine("64 Bit-OS: {0}", System.Environment.Is64BitOperatingSystem ? "ja" : "nein");
            Console.Error.WriteLine("Programmodus 64 Bit: {0}", System.Environment.Is64BitProcess ? "ja" : "nein");

            Console.Error.WriteLine("MinorDistance:           {0}", opt.MinorDistance);
            Console.Error.WriteLine("MediumFactor:            {0}", opt.MediumFactor);
            Console.Error.WriteLine("MajorFactor:             {0}", opt.MajorFactor);

            Console.Error.WriteLine("HGT-Verzeichnis:         {0}", opt.HGTPath);
            Console.Error.WriteLine("Mergefile:               {0}", opt.Mergefile);
            Console.Error.WriteLine("OutputOverwrite:         {0}", opt.OutputOverwrite);
            Console.Error.WriteLine("Höhenkorrektur:          {0}", opt.FakeDistance);
            Console.Error.WriteLine("MinVerticePoints:        {0}", opt.MinVerticePoints);
            Console.Error.WriteLine("MinBoundingbox:          {0}", opt.MinBoundingbox);
            Console.Error.WriteLine("Douglas-Peucker:         {0}", opt.DouglasPeucker);
            Console.Error.WriteLine("1. OSM-ID:               {0}", opt.FirstID);

            Console.Error.WriteLine("mit OSMBound:            {0}", opt.OSMBound);
            Console.Error.WriteLine("mit OSMBounds:           {0}", opt.OSMBounds);
            Console.Error.WriteLine("mit OSMTimestamp:        {0}", opt.OSMTimestamp);
            if (opt.OSMUser.Length > 0)
               Console.Error.WriteLine("mit OSMUser:             {0}", opt.OSMUser);
            if (opt.OSMVersion > 0)
               Console.Error.WriteLine("mit OSMVersion:          {0}", opt.OSMVersion);
            Console.Error.WriteLine("mit OSMVisible:          {0}", opt.OSMVisible);

            Console.Error.WriteLine("max. Punktanzahl je Weg: {0}", opt.MaxNodesPerWay);
            Console.Error.WriteLine("mit 'contour_ext'-Tag:   {0}", opt.WriteElevationType);

            double dMinLat = Double.MaxValue;
            double dMaxLat = Double.MinValue;
            double dMinLon = Double.MaxValue;
            double dMaxLon = Double.MinValue;
            long FirstID = opt.FirstID;
            List<string> Outfiles = new List<string>();

            int[] Latitude = opt.Latitude;
            int[] Longitude = opt.Longitude;

            if (Latitude.Length == 0)
               GetLatLon(opt.HGTPath, out Longitude, out Latitude);

            for (int i = 0; i < Latitude.Length; i++) {
               string sOutfile = opt.Outfile.Length > i ? opt.Outfile[i] : "";
               if (opt.FirstID < 0) {     // dann aus den Werten opt.Latitude[i] und opt.Longitude[i] bilden
                  FirstID = 1000 * (Latitude[i] + 90) + Longitude[i] + 180;
                  FirstID *= 10000000000;    // multipl. mit 10^10 --> Bereich 10^10 bis 180360*10^10 wird verwendet
               }
               long newFirstID = Do4Tile(opt, Latitude[i], Longitude[i], FirstID, ref sOutfile);
               if (FirstID != newFirstID) {      // sonst ist ein Fehler aufgetreten (z.B. ex. die Ausgangsdaten nicht)
                  // Daten für die Bounding-Box ermitteln
                  if (dMinLat > Latitude[i])
                     dMinLat = Latitude[i];
                  if (dMaxLat < Latitude[i])
                     dMaxLat = Latitude[i];
                  if (dMinLon > Longitude[i])
                     dMinLon = Longitude[i];
                  if (dMaxLon < Longitude[i])
                     dMaxLon = Longitude[i];
                  Outfiles.Add(sOutfile);
               } else
                  Outfiles.Add(null);
            }

            if (opt.Mergefile.Length > 0) {
               dMaxLat += 1;
               dMaxLon += 1;
               Create2Mergefile(opt.Mergefile, Outfiles, opt.OSMBound, opt.OSMBounds, dMinLon, dMaxLon, dMinLat, dMaxLat);
            }

         } catch (Exception ex) {
            Console.Error.WriteLine("Exception: " + ex.Message);
         }
      }

      /// <summary>
      /// liefert die Lon/Lat-Werte aller Dateien in dem Verzeichnis
      /// </summary>
      /// <param name="hgtdir"></param>
      /// <param name="lon"></param>
      /// <param name="lat"></param>
      static void GetLatLon(string hgtdir, out int[] lon, out int[] lat) {
         List<int> Lon = new List<int>(), Lat = new List<int>();
         if (hgtdir != "") {
            foreach (string filename in Directory.EnumerateFiles(hgtdir)) {
               string file = Path.GetFileName(filename);
               int dot = file.IndexOf('.');
               if (dot >= 0) {
                  string basename = file.Substring(0, dot).ToLower();
                  string ext = file.Substring(dot).ToLower();
                  if (ext == ".hgt" ||
                      ext == ".hgt.zip") {
                     if (basename.Length == 7 &&
                         (basename[0] == 'n' || basename[0] == 's') &&
                         char.IsDigit(basename[1]) &&
                         char.IsDigit(basename[2]) &&
                         (basename[3] == 'e' || basename[3] == 'w') &&
                         char.IsDigit(basename[4]) &&
                         char.IsDigit(basename[5]) &&
                         char.IsDigit(basename[6])) {
                        int tmp;
                        tmp = Convert.ToInt16(basename.Substring(1, 2));
                        if (basename[0] == 's')
                           tmp = -tmp;
                        Lat.Add(tmp);
                        tmp = Convert.ToInt16(basename.Substring(4, 3));
                        if (basename[3] == 'w')
                           tmp = -tmp;
                        Lon.Add(tmp);
                     }
                  }
               }
            }
         }
         lon = Lon.ToArray();
         lat = Lat.ToArray();
      }

      /// <summary>
      /// erzeugt aus den Daten im Contour-Prozessor eine Datei im Format "ArcInfo ASCII Grid"
      /// </summary>
      /// <param name="filename"></param>
      /// <param name="cp"></param>
      static void CreateArcInfoASCIIGrid(string filename, ContourProcessor2 cp) {
         /*
            ncols xxxxx
            ncols refers to the number of columns in the grid and xxxxx is the numerical value

            nrows xxxxx
            nrows refers to the number of rows in the grid and xxxxx is the numerical value

            xllcorner xxxxx
            xllcorner refers to the western edge of the grid and xxxxx is the numerical value
            xllcorner and yllcorner are given as the EDGES of the grid, NOT the centers of the edge cells.
            The origin of the grid is the upper left and terminus at the lower right.

            yllcorner xxxxx
            yllcorner refers to the southern edge of the grid and xxxxx is the numerical value

            cellsize xxxxx
            cellsize refers to the resolution of the grid and xxxxx is the numerical value

            nodata_value xxxxx
            nodata_value refers to the value that represents missing data and xxxxx is the numerical value. This is
            optional and your parser should not assume it will be present. Note: that if you need a good value, the ESRI default is -9999.
          * 
          * Beispiel:
               ncols 157
               nrows 171
               xllcorner -156.08749650000
               yllcorner 18.870890200000
               cellsize 0.00833300
               0 0 1 1 1 2 3 3 5 6 8 9 12 14 18 21 25 30 35 41 47 53
               59 66 73 79 86 92 97 102 106 109 112 113 113 113 111 109 106
               103 98 94 89 83 78 72 67 61 56 51 46 41 37 32 29 25 22 19
               etc...
          */

         using (StreamWriter sw = new StreamWriter(filename)) {
            short nodata = -29999;

            Console.Error.WriteLine("schreibe Datei '{0}' ...", filename);
            sw.WriteLine("ncols {0}", cp.Width.ToString());
            sw.WriteLine("nrows {0}", cp.Height.ToString());
            //sw.WriteLine("xllcorner {0}", (cp.Left + 1.0 / (2 * cp.Width)).ToString(CultureInfo.InvariantCulture));
            //sw.WriteLine("yllcorner {0}", (cp.Bottom + 1.0 / (2 * cp.Height)).ToString(CultureInfo.InvariantCulture));
            sw.WriteLine("xllcorner {0}", cp.Left.ToString(CultureInfo.InvariantCulture));
            sw.WriteLine("yllcorner {0}", cp.Bottom.ToString(CultureInfo.InvariantCulture));
            sw.WriteLine("cellsize {0}", (1.0 / cp.Width).ToString(CultureInfo.InvariantCulture));
            sw.WriteLine("nodata_value {0}", nodata);
            for (int r = 0; r < cp.Height; r++) {
               for (int c = 0; c < cp.Width; c++) {
                  int v = cp.Get(c, r);
                  if (v == HGTReader.NoValue)
                     v = nodata;
                  sw.Write(" " + v.ToString());
               }
               sw.WriteLine();
            }
         }

      }

      /// <summary>
      /// erzeugt eine Geo-PNG-Datei aus den HGT-Daten
      /// </summary>
      /// <param name="r"></param>
      /// <param name="lat"></param>
      /// <param name="lon"></param>
      /// <param name="GeoColor"></param>
      /// <param name="DummyGeoColor"></param>
      static void CreateGeofile(HGTReader r, int lat, int lon, SortedDictionary<int, Color> GeoColor, Color DummyGeoColor) {
         string bitmapfile = string.Format("Height_{0}{1:D2}{2}{3:D3}.png",
                                                      lat >= 0 ? "N" : "S", lat,
                                                      lon >= 0 ? "E" : "W", lon);
         Bitmap bm;
         if (GeoColor.Count > 2)
            bm = r.GetBitmap(GeoColor, DummyGeoColor);
         else
            bm = r.GetBitmap();

         bm.Save(bitmapfile, System.Drawing.Imaging.ImageFormat.Png);
         //string worldfile = Path.GetFileNameWithoutExtension(bitmapfile) + ".pnw";
         string worldfile = bitmapfile + "w";            // Quantum-GIS will es so
         using (StreamWriter sw = new StreamWriter(worldfile)) {
            sw.WriteLine("{0}", (1 / (double)bm.Width).ToString(CultureInfo.InvariantCulture));
            sw.WriteLine("0");
            sw.WriteLine("0");
            sw.WriteLine("{0}", (-1 / (double)bm.Height).ToString(CultureInfo.InvariantCulture));    // negativ, weil die Zeilen von oben nach unten enthalten sind
            sw.WriteLine("{0}", lon);
            sw.WriteLine("{0}", lat + 1);       // oberer Rand
         }
         Console.Error.WriteLine("Datei {0} geschrieben", bitmapfile);

      }

      static void Create2Mergefile(string mergefile, IList<string> osmfile, bool bBound,
                                  bool bBounds, double dMinLon, double dMaxLon, double dMinLat, double dMaxLat) {
         Console.Error.WriteLine("erzeuge Gesamt-OSM-Datei '{0}' ...", mergefile);
         Encoding StdOutEncoding = Console.OutputEncoding;

         if (mergefile != "-") {
            StreamWriter merge;
            if (Path.GetExtension(mergefile).ToLower() == ".gz") {
               FileStream fs1 = new FileStream(mergefile, FileMode.Create);
               merge = new StreamWriter((Stream)new GZipStream(fs1, CompressionMode.Compress));
            } else
               merge = new StreamWriter(mergefile, false, Encoding.UTF8);
            // STDOUT auf Datei umlenken
            Console.Out.Flush();
            merge.AutoFlush = true;
            Console.SetOut(merge);
         } else {
            Console.OutputEncoding = Encoding.UTF8;
         }

         Console.WriteLine("<?xml version='1.0' encoding='UTF-8'?>");
         Console.WriteLine("<osm version='0.6' generator='HGT2OSM'>");
         if (bBounds)
            Console.WriteLine("<bounds minlat='{0}' minlon='{1}' maxlat='{2}' maxlon='{3}'/>",
                              dMinLat.ToString(CultureInfo.InvariantCulture),
                              dMinLon.ToString(CultureInfo.InvariantCulture),
                              dMaxLat.ToString(CultureInfo.InvariantCulture),
                              dMaxLon.ToString(CultureInfo.InvariantCulture));
         else
            if (bBound)
            Console.WriteLine("<bound box='{0},{1},{2},{3}' origin='http://dds.cr.usgs.gov/srtm/version2_1'/>",
                              dMinLat.ToString(CultureInfo.InvariantCulture),
                              dMinLon.ToString(CultureInfo.InvariantCulture),
                              dMaxLat.ToString(CultureInfo.InvariantCulture),
                              dMaxLon.ToString(CultureInfo.InvariantCulture));

         long[] waypos = new long[osmfile.Count];
         for (int i = 0; i < osmfile.Count; i++) {
            if (osmfile[i] == null) continue;
            Console.Error.WriteLine("hole Nodes aus '{0}' ...", osmfile[i]);

            FileStream fsr = new FileStream(osmfile[i], FileMode.Open, FileAccess.Read);
            GZipStream decompress = Path.GetExtension(osmfile[i]).ToLower() == ".gz" ?
                                          new GZipStream(fsr, CompressionMode.Decompress) : null;
            using (StreamReader sr = new StreamReader(decompress != null ? (Stream)decompress : fsr)) {
               string line;
               sr.ReadLine();    // <?xml...
               sr.ReadLine();    // <osm...
               do {
                  if (sr.BaseStream.CanSeek)
                     waypos[i] = sr.BaseStream.Position;
                  line = sr.ReadLine();
                  if (line != null && line.Length > 3 &&
                      line.Substring(0, 3) == "<no")
                     Console.WriteLine(line);
                  else
                     if (line.Substring(0, 3) == "<wa")
                     break;
               } while (line != null);
            }
         }

         for (int i = 0; i < osmfile.Count; i++) {
            if (osmfile[i] == null) continue;
            Console.Error.WriteLine("hole Ways aus '{0}' ...", osmfile[i]);
            FileStream fsr = new FileStream(osmfile[i], FileMode.Open, FileAccess.Read);
            GZipStream decompress = Path.GetExtension(osmfile[i]).ToLower() == ".gz" ?
                                          new GZipStream(fsr, CompressionMode.Decompress) : null;
            using (StreamReader sr = new StreamReader(decompress != null ? (Stream)decompress : fsr)) {
               string line;
               if (sr.BaseStream.CanSeek)
                  sr.BaseStream.Seek(waypos[i], SeekOrigin.Begin);
               do {
                  line = sr.ReadLine();
                  if (line != null && line.Length > 3 &&
                      (line.Substring(0, 3) == "<wa" ||
                       line.Substring(0, 3) == "<nd" ||
                       line.Substring(0, 3) == "<ta" ||
                       line.Substring(0, 3) == "</w"))
                     Console.WriteLine(line);
               } while (line != null);
            }
         }

         Console.WriteLine("</osm>");

         if (mergefile != "-") {
            // STDOUT wieder auf Konsole lenken
            Console.Out.Flush();
            Console.Out.Close();
            StreamWriter output = new StreamWriter(Console.OpenStandardOutput(), StdOutEncoding);
            output.AutoFlush = true;
            Console.SetOut(output);
         } else
            Console.OutputEncoding = StdOutEncoding;

      }

      static long Do4Tile(Options opt, int lat, int lon, long FirstID, ref string sOutfile) {
         long LastID = FirstID;
         try {

            Console.Error.WriteLine("==> Lese Ausgangsdaten {0}° {1}° ...", lat, lon);
            HGTReader r = new HGTReader(lon, lat, opt.HGTPath);

            if (opt.OnlyGeoPng) {      // erzeugt nur ein georef. Bitmap

               CreateGeofile(r, lat, lon, opt.GeoColor, opt.DummyGeoColor);
               return 0;

            } else {

               Console.Error.WriteLine("Daten übernehmen ...");
               ContourProcessor2 cp = new ContourProcessor2(r, opt.MaxThreads);
               if (opt.OnlyArcInfoASCIIGrid) {

                  CreateArcInfoASCIIGrid(string.Format("Height_{0}{1:D2}{2}{3:D3}.asc",
                                                                lat >= 0 ? "N" : "S", lat,
                                                                lon >= 0 ? "E" : "W", lon),
                                         cp);
                  return 0;

               } else {
                  cp.FirstID = FirstID;
                  if (sOutfile.Length > 0)
                     cp.Outfile = sOutfile;
                  sOutfile = cp.Outfile;
                  cp.ShowPoints = opt.ShowPoints;
                  cp.ShowAreas = opt.ShowAreas;
                  cp.OSMBound = opt.OSMBound;
                  cp.OSMBounds = opt.OSMBounds;
                  cp.OSMTimestamp = opt.OSMTimestamp;
                  cp.OSMUser = opt.OSMUser;
                  cp.OSMVersion = opt.OSMVersion;
                  cp.OSMVisible = opt.OSMVisible;
                  if (File.Exists(cp.Outfile) && !opt.OutputOverwrite) {
                     Console.Error.WriteLine("Zieldatei '{0}' existiert schon ... Abbruch", cp.Outfile);
                     FileStream fsr = new FileStream(cp.Outfile, FileMode.Open, FileAccess.Read);
                     GZipStream decompress = Path.GetExtension(cp.Outfile).ToLower() == ".gz" ?
                                                   new GZipStream(fsr, CompressionMode.Decompress) :
                                                   null;
                     using (StreamReader sr = new StreamReader(decompress != null ? (Stream)decompress : fsr)) {
                        string line;
                        sr.ReadLine();    // <?xml...
                        sr.ReadLine();    // <osm...
                        do {
                           line = sr.ReadLine();
                           if (line != null && line.Length > 3 &&
                               line.Substring(0, 3) == "<no") {
                              // <node id='1010000014' 
                              line = line.Substring(10);
                              int pos = line.IndexOf('\'');
                              if (pos > 0)
                                 LastID = Convert.ToInt32(line.Substring(0, pos));
                           } else
                              break;
                        } while (line != null);
                     }

                     return LastID;
                  }

                  cp.CreateIsohypsen((short)opt.MinorDistance,
                                     opt.MediumFactor * opt.MinorDistance,
                                     opt.MajorFactor * opt.MediumFactor * opt.MinorDistance,
                                     opt.MaxNodesPerWay,
                                     opt.WriteElevationType,
                                     opt.FakeDistance,
                                     opt.MinVerticePoints,
                                     opt.MinBoundingbox,
                                     opt.DouglasPeucker,
                                     opt.LineBitmapWidth,
                                     opt.PolylineBitmapWidth,
                                     opt.Textdata);

                  Console.Error.WriteLine("verwendete OSM-ID's: {0} .. {1}", cp.FirstID, cp.LastID);
                  Console.Error.WriteLine("==> OSM-Datei: {0}", cp.Outfile);
                  LastID = cp.LastID;
               }
               cp = null;
               GC.Collect();

            }
         } catch (Exception ex) {
            Console.Error.WriteLine("Fehler: " + ex.Message);
            Console.Error.WriteLine(ex.StackTrace);
            LastID = FirstID;                            // es sind keine ID's dazu gekommen
         }
         return LastID;
      }

      /// <summary>
      /// liefert den Titel des Programms
      /// </summary>
      /// <param name="a"></param>
      /// <returns></returns>
      static string Title() {
         Assembly a = Assembly.GetExecutingAssembly();
         string sTitle = "";
         object[] attributes = a.GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
         if (attributes.Length > 0) {
            AssemblyTitleAttribute titleAttribute = (AssemblyTitleAttribute)attributes[0];
            if (titleAttribute.Title != "")
               sTitle = titleAttribute.Title;
         }
         if (sTitle.Length == 0)         // Notlösung
            sTitle = System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().CodeBase);
         //string sVersion = a.GetName().Version.ToString();
         string sInfoVersion = "";
         attributes = a.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false);
         if (attributes.Length > 0)
            sInfoVersion = ((AssemblyInformationalVersionAttribute)attributes[0]).InformationalVersion;
         string sCompany = "";
         attributes = a.GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
         if (attributes.Length > 0)
            sCompany = ((AssemblyCompanyAttribute)attributes[0]).Company;

         return sTitle + ", " + sInfoVersion + ", " + sCompany;
      }

      static HGTReader Test1(int lat, int lon) {
         return new HGTReader(lon, lat, new short[]{
            24, 10, 10, 10, 10, 24,
            24, 10, 10, 10, 10, 24,
            10, 10, 10, 10, 24, 24,
            10, 10, 10, 10, 24, 24,
            24, 10, 10, 10, 10, 24,
            24, 24, 10, 10, 10, 24,
         });
      }
      static HGTReader Test2(int lat, int lon) {
         return new HGTReader(lon, lat, new short[]{
            24, 10, 10, 10, 24,
            24, 10, 10, 10, 24,
            10, 10, 10, 24, 24,
            24, 10, 10, 10, 24,
            24, 24, 10, 10, 24,
         });
      }
      static HGTReader Test3(int lat, int lon) {
         // -g ..\.. -p 0 -o hl1.osm -d 0 --NoLowWaveFilter -a 0 --MinVerticePoints=2 --MinBoundingbox=0
         return new HGTReader(lon, lat, new short[]{
             0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
             0, 0, 0, 0, 0, 0, 0, 0,22,22,22, 0, 0, 0, 0, 0, 0,
             0, 0, 0, 0, 0,22,22,22,22,22,22,22,22,22, 0, 0, 0,
             0, 0, 0, 0, 0, 0,22,22,22,22,22,22,22,22,22, 0, 0,
             0, 0, 0, 0, 0, 0, 0,22,22,22,22,22,22,22,22, 0, 0,
             0, 0, 0, 0, 0, 0,22,22,22,22,22,22,22,22,22, 0, 0,
             0, 0, 0, 0, 0, 0,22,22,22,22,22,22,22,22,22, 0, 0,
             0, 0, 0, 0, 0,22,22,22,22,22,22,22,22,22,22, 0, 0,
             0, 0, 0,22,22,22,22,22,22,22,22,22,22,22,22, 0, 0,
             0, 0, 0,22,22,22,22,22,22,22,22,22,22,22,22, 0, 0,
             0, 0, 0,22,22,22,22,22,22,22,22,22,22,22,22, 0, 0,
             0, 0,22,22,22,22,22,22,22,22,22,22,22,22,22, 0, 0,
             0,22,22,22,22,22,22,22,22,22,22,22,22,22,22,22, 0,
             0, 0,22,22,22,22,22,22,22,22,22,22,22,22,22, 0, 0,
             0, 0, 0, 0, 0, 0, 0, 0,22,22,22,22, 0, 0, 0, 0, 0,
             0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
             0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
         });
      }


   }
}
