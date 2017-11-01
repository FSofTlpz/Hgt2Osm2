// FSofT, 5.7.2010, 5.3.2011

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
using System.Text.RegularExpressions;
using System.Text;
using System.Globalization;

namespace FsoftUtils {

   /*
    * Eine Option kann in kurzer (-x) und/oder langer (--lang) Form definiert werden.
    * Gültige Optionen müßen den reg. Ausdrücken rex_opt_short bzw. rex_opt_long entsprechen.
    * Ein Argument für eine Option wird durch Leerzeichen oder bei langen Optionen auch durch ein '=' von der Option getrennt.
    * Optionen müßen eindeutig sein.
    */

   public class CmdlineOptions {

      public enum OptionArgumentType { Nothing, String, Integer, UnsignedInteger, PositivInteger, Long, UnsignedLong, PositivLong, Double, Boolean }

      //Regex rex_opt_short = new Regex(@"^-([\w\?]{1})$");
      Regex rex_opt_short = new Regex(@"^-([A-Za-z0-9\?]+)$");
      Regex rex_opt_long = new Regex(@"^(--|/)([A-Za-z0-9]{1}[\w_]*)");     // \w <--> [A-Za-z0-9_] (vgl. http://en.wikipedia.org/wiki/Regular_expression)

      protected class OptionDefinition {
         public int iKey { get; private set; }
         public string sLongOption { get; private set; }
         public string sShortOption { get; private set; }
         public OptionArgumentType Typ { get; private set; }
         public int iMaxCount { get; private set; }
         public string sHelpText { get; private set; }

         public OptionDefinition(int uniquekey, string sLongOption, string sShortOption, string sHelpText, OptionArgumentType argtype, int maxcount) {
            iKey = uniquekey;
            this.Typ = argtype;
            this.sLongOption = "";
            this.sShortOption = "";
            this.sHelpText = "";
            this.iMaxCount = 1;
            if (sLongOption == null || sShortOption == null || (sLongOption.Trim().Length == 0 && sShortOption.Trim().Length == 0))
               throw new ArgumentException(string.Format("Die Option '{0}' ist unsinnig.", this.ToString()));
            this.sLongOption = sLongOption;
            this.sShortOption = sShortOption;
            if (sShortOption.Length > 1)
               throw new ArgumentException(string.Format("Der kurze Text der Option in '{0}' ist zu lang.", this.ToString()));
            this.sHelpText = sHelpText;
            this.iMaxCount = maxcount;
            if (iMaxCount <= 0)
               throw new ArgumentException(string.Format("Die Optionsanzahl der Option '{0}' ist unsinnig.", this.ToString()));
         }

         public string LongName() {
            return sLongOption.Length > 0 ? "--" + sLongOption : "";
         }

         public string ShortName() {
            return sShortOption.Length > 0 ? "-" + sShortOption : "";
         }

         public string Name() {
            return LongName().Length > 0 && ShortName().Length > 0 ?
               string.Format("{0}, {1}", ShortName(), LongName()) :
               LongName().Length > 0 ? LongName() : ShortName();
         }

         public override string ToString() {
            return string.Format("{0} (Key={1}, Typ={2}, iMaxCount={3})", Name(), iKey, Typ, iMaxCount);
         }
      }

      protected Dictionary<int, OptionDefinition> DefinedOptions;

      /// <summary>
      /// eine eingelesene Option
      /// </summary>
      protected class SampledOption {
         public int iKey { get; private set; }
         public string sOption { get; private set; }
         public bool bShort { get; private set; }
         public OptionArgumentType Typ { get; private set; }
         public object oArgument { get; private set; }

         public SampledOption(int key, string sOption, bool bShort, OptionArgumentType Typ, string sArgument) {
            iKey = key;
            this.sOption = sOption;
            this.bShort = bShort;
            this.Typ = Typ;
            oArgument = null;

            switch (Typ) {
               case OptionArgumentType.Nothing:
                  if (sArgument != null)
                     throw new Exception(string.Format("Die Kommandozeilenoption '{0}' darf kein Argument haben.", Name()));
                  break;

               case OptionArgumentType.String:
                  if (sArgument == null)
                     throw new Exception(string.Format("Die Kommandozeilenoption '{0}' muss ein Argument haben.", Name()));
                  oArgument = sArgument;
                  break;

               case OptionArgumentType.Double:
                  double dArg;
                  if (!DoubleIsPossible(sArgument, out dArg)) ThrowExceptionFalseType2();
                  oArgument = dArg;
                  break;

               case OptionArgumentType.Integer:
                  int iArg;
                  if (!IntegerIsPossible(sArgument, out iArg)) ThrowExceptionFalseType2();
                  oArgument = iArg;
                  break;

               case OptionArgumentType.UnsignedInteger:
                  uint uArg;
                  if (!UnsignedIntegerIsPossible(sArgument, out uArg)) ThrowExceptionFalseType2();
                  oArgument = uArg;
                  break;

               case OptionArgumentType.PositivInteger:
                  if (!UnsignedIntegerIsPossible(sArgument, out uArg) || uArg == 0) ThrowExceptionFalseType2();
                  oArgument = uArg;
                  break;

               case OptionArgumentType.Long:
                  long lArg;
                  if (!LongIsPossible(sArgument, out lArg)) ThrowExceptionFalseType2();
                  oArgument = lArg;
                  break;

               case OptionArgumentType.UnsignedLong:
                  ulong ulArg;
                  if (!UnsignedLongIsPossible(sArgument, out ulArg)) ThrowExceptionFalseType2();
                  oArgument = ulArg;
                  break;

               case OptionArgumentType.PositivLong:
                  if (!UnsignedLongIsPossible(sArgument, out ulArg) || ulArg == 0) ThrowExceptionFalseType2();
                  oArgument = ulArg;
                  break;

               case OptionArgumentType.Boolean:
                  bool bArg;
                  if (!BooleanIsPossible(sArgument, out bArg)) ThrowExceptionFalseType2();
                  oArgument = bArg;
                  break;
            }
         }

         /// <summary>
         /// Kann das Argument als Double interpretiert werden?
         /// </summary>
         /// <param name="sArgument"></param>
         /// <param name="val"></param>
         /// <returns></returns>
         public static bool DoubleIsPossible(string sArgument, out double val) {
            bool bPossible = true;
            val = 0;
            try {
               val = Convert.ToDouble(sArgument, CultureInfo.InvariantCulture);
            } catch {
               bPossible = false;
            }
            return bPossible;
         }

         /// <summary>
         /// Kann das Argument als Integer (auch hexadezimal mit 0x) interpretiert werden?
         /// </summary>
         /// <param name="sArgument"></param>
         /// <param name="val"></param>
         /// <returns></returns>
         public static bool IntegerIsPossible(string sArgument, out int val) {
            bool bPossible = true;
            val = 0;
            try {
               val = Convert.ToInt32(sArgument);
            } catch {
               bPossible = false;
            }
            if (!bPossible)
               try {
                  bPossible = true;
                  val = Convert.ToInt32(sArgument, 16);     // Test auf Hex-Zahl
               } catch {
                  bPossible = false;
               }
            return bPossible;
         }

         /// <summary>
         /// Kann das Argument als UInteger (auch hexadezimal mit 0x) interpretiert werden?
         /// </summary>
         /// <param name="sArgument"></param>
         /// <param name="val"></param>
         /// <returns></returns>
         public static bool UnsignedIntegerIsPossible(string sArgument, out uint val) {
            bool bPossible = true;
            val = 0;
            try {
               val = Convert.ToUInt32(sArgument);
            } catch {
               bPossible = false;
            }
            if (!bPossible)
               try {
                  bPossible = true;
                  val = Convert.ToUInt32(sArgument, 16);     // Test auf Hex-Zahl
               } catch {
                  bPossible = false;
               }
            return bPossible;
         }

         /// <summary>
         /// Kann das Argument als Long (auch hexadezimal mit 0x) interpretiert werden?
         /// </summary>
         /// <param name="sArgument"></param>
         /// <param name="val"></param>
         /// <returns></returns>
         public static bool LongIsPossible(string sArgument, out long val) {
            bool bPossible = true;
            val = 0;
            try {
               val = Convert.ToInt64(sArgument);
            } catch {
               bPossible = false;
            }
            if (!bPossible)
               try {
                  bPossible = true;
                  val = Convert.ToInt64(sArgument, 16);     // Test auf Hex-Zahl
               } catch {
                  bPossible = false;
               }
            return bPossible;
         }

         /// <summary>
         /// Kann das Argument als ULong (auch hexadezimal mit 0x) interpretiert werden?
         /// </summary>
         /// <param name="sArgument"></param>
         /// <param name="val"></param>
         /// <returns></returns>
         public static bool UnsignedLongIsPossible(string sArgument, out ulong val) {
            bool bPossible = true;
            val = 0;
            try {
               val = Convert.ToUInt64(sArgument);
            } catch {
               bPossible = false;
            }
            if (!bPossible)
               try {
                  bPossible = true;
                  val = Convert.ToUInt64(sArgument, 16);     // Test auf Hex-Zahl
               } catch {
                  bPossible = false;
               }
            return bPossible;
         }

         /// <summary>
         /// Kann das Argument als Boolean (auch hexadezimal mit 0x) interpretiert werden?
         /// </summary>
         /// <param name="sArgument"></param>
         /// <param name="val"></param>
         /// <returns></returns>
         public static bool BooleanIsPossible(string sArgument, out bool val) {
            bool bPossible = true;
            val = true;
            try {
               val = Convert.ToBoolean(sArgument);          // fkt. nur bei "true" oder "false"
            } catch {
               bPossible = false;
            }
            if (!bPossible)
               try {
                  bPossible = true;
                  int iVal;
                  double dVal;
                  if (IntegerIsPossible(sArgument, out iVal))
                     val = Convert.ToBoolean(iVal);
                  else
                     if (DoubleIsPossible(sArgument, out dVal))
                        val = Convert.ToBoolean(dVal);
                     else
                        bPossible = false;
               } catch {
                  bPossible = false;
               }
            return bPossible;
         }

         protected void ThrowExceptionNoInterpret() {
            throw new Exception(string.Format("Die Kommandozeilenoption '{0}' kann nicht als '{1}' interpretiert werden.", FullName(), Typ));
         }

         protected void ThrowExceptionFalseType() {
            throw new Exception(string.Format("Die Kommandozeilenoption '{0}' wurde nicht als '{1}' festgelegt.", FullName(), Typ));
         }

         protected void ThrowExceptionFalseType2() {
            throw new Exception(string.Format("Die Kommandozeilenoption '{0}' muss ein {1}-Argument haben.", FullName(), Typ));
         }

         /// <summary>
         /// Wurde die Option verwendet (natürlich)?
         /// </summary>
         /// <returns></returns>
         public bool AsUsed(bool bLazy) {
            if (Typ == OptionArgumentType.Nothing || bLazy)
               return true;
            ThrowExceptionFalseType();
            return false;
         }

         /// <summary>
         /// liefert das Argument wenn möglich als Integer
         /// </summary>
         /// <returns></returns>
         public int AsInteger(bool bLazy) {
            int val;
            if (Typ == OptionArgumentType.Integer)
               return Convert.ToInt32(oArgument);
            else {
               if (bLazy) {
                  if (IntegerIsPossible(oArgument.ToString(), out val))
                     return val;
                  ThrowExceptionNoInterpret();
               }
               ThrowExceptionFalseType();
            }
            return 0;
         }

         /// <summary>
         /// liefert das Argument wenn möglich als UnsignedInteger
         /// </summary>
         /// <returns></returns>
         public uint AsUnsignedInteger(bool bLazy) {
            uint val;
            if (Typ == OptionArgumentType.UnsignedInteger)
               return Convert.ToUInt32(oArgument);
            else {
               if (bLazy) {
                  if (UnsignedIntegerIsPossible(oArgument.ToString(), out val))
                     return val;
                  ThrowExceptionNoInterpret();
               }
               ThrowExceptionFalseType();
            }
            return 0;
         }

         /// <summary>
         /// liefert das Argument wenn möglich als PositivInteger
         /// </summary>
         /// <returns></returns>
         public uint AsPositivInteger(bool bLazy) {
            uint val;
            if (Typ == OptionArgumentType.PositivInteger) {
               val = Convert.ToUInt32(oArgument);
               if (val > 0)
                  return val;
            }
            if (bLazy) {
               if (UnsignedIntegerIsPossible(oArgument.ToString(), out val) && val > 0)
                  return val;
               ThrowExceptionNoInterpret();
            }
            ThrowExceptionFalseType();
            return 0;
         }

         /// <summary>
         /// liefert das Argument wenn möglich als Long
         /// </summary>
         /// <returns></returns>
         public long AsLong(bool bLazy) {
            long val;
            if (Typ == OptionArgumentType.Long)
               return Convert.ToInt64(oArgument);
            else {
               if (bLazy) {
                  if (LongIsPossible(oArgument.ToString(), out val))
                     return val;
                  ThrowExceptionNoInterpret();
               }
               ThrowExceptionFalseType();
            }
            return 0;
         }

         /// <summary>
         /// liefert das Argument wenn möglich als UnsignedLong
         /// </summary>
         /// <returns></returns>
         public ulong AsUnsignedLong(bool bLazy) {
            ulong val;
            if (Typ == OptionArgumentType.UnsignedInteger)
               return Convert.ToUInt64(oArgument);
            else {
               if (bLazy) {
                  if (UnsignedLongIsPossible(oArgument.ToString(), out val))
                     return val;
                  ThrowExceptionNoInterpret();
               }
               ThrowExceptionFalseType();
            }
            return 0;
         }

         /// <summary>
         /// liefert das Argument wenn möglich als PositivLong
         /// </summary>
         /// <returns></returns>
         public ulong AsPositivLong(bool bLazy) {
            ulong val;
            if (Typ == OptionArgumentType.PositivInteger) {
               val = Convert.ToUInt64(oArgument);
               if (val > 0)
                  return val;
            }
            if (bLazy) {
               if (UnsignedLongIsPossible(oArgument.ToString(), out val) && val > 0)
                  return val;
               ThrowExceptionNoInterpret();
            }
            ThrowExceptionFalseType();
            return 0;
         }

         /// <summary>
         /// liefert das Argument wenn möglich als Double
         /// </summary>
         /// <returns></returns>
         public double AsDouble(bool bLazy) {
            double val;
            if (Typ == OptionArgumentType.Double)
               return Convert.ToDouble(oArgument, CultureInfo.InvariantCulture);
            else {
               if (bLazy) {
                  if (DoubleIsPossible(oArgument.ToString(), out val))
                     return val;
                  ThrowExceptionNoInterpret();
               }
               ThrowExceptionFalseType();
            }
            return 0;
         }

         /// <summary>
         /// liefert das Argument wenn möglich als String
         /// </summary>
         /// <returns></returns>
         public string AsString() {
            if (Typ != OptionArgumentType.Nothing)
               return Convert.ToString(oArgument);
            else
               ThrowExceptionNoInterpret();
            return null;
         }

         /// <summary>
         /// liefert das Argument wenn möglich als Boolean
         /// </summary>
         /// <returns></returns>
         public bool AsBoolean(bool bLazy) {
            bool val;
            if (Typ == OptionArgumentType.Boolean)
               return Convert.ToBoolean(oArgument);
            else {
               if (bLazy) {
                  if (BooleanIsPossible(oArgument.ToString(), out val))
                     return val;
                  ThrowExceptionNoInterpret();
               }
               ThrowExceptionFalseType();
            }
            return true;
         }

         /// <summary>
         /// Name der Option mit führendem '-' oder '--'
         /// </summary>
         /// <returns></returns>
         public string Name() {
            return string.Format("{0}{1}", bShort ? "-" : "--", sOption);
         }

         /// <summary>
         /// Name der Option mit ev. vorhandenem Argument
         /// </summary>
         /// <returns></returns>
         public string FullName() {
            return oArgument != null ? Name() + " " + oArgument.ToString() : Name();
         }

         public override string ToString() {
            return string.Format("{0} (Key={1}, Typ={2}, Argument={3})", Name(), iKey, Typ, oArgument == null ? "[null]" : oArgument.ToString());
         }
      }

      protected List<SampledOption> SampledOptions;


      public CmdlineOptions() {
         DefinedOptions = new Dictionary<int, OptionDefinition>();
         ClearParse();
      }

      #region Definition erlaubter Optionen

      public void DefineOption(int uniquekey, string sLongOption, string sShortOption, string sHelpText) {
         DefineOption(uniquekey, sLongOption, sShortOption, sHelpText, OptionArgumentType.Nothing, 1);
      }
      public void DefineOption(int uniquekey, string sLongOption, string sShortOption, string sHelpText, OptionArgumentType argtype) {
         DefineOption(uniquekey, sLongOption, sShortOption, sHelpText, argtype, 1);
      }
      /// <summary>
      /// eine neue Option definieren
      /// </summary>
      /// <param name="uniquekey">eindeutiger int-Schlüssel</param>
      /// <param name="sLongOption">langer Name (oder "")</param>
      /// <param name="sShortOption">kurzer Name (oder "")</param>
      /// <param name="sHelpText">Hilfetext (Umbruch jeweils bei \n)</param>
      /// <param name="argtype">Art der Option</param>
      /// <param name="maxcount">max. Anzahl des Auftretens der Option</param>
      public void DefineOption(int uniquekey, string sLongOption, string sShortOption, string sHelpText, OptionArgumentType argtype, int maxcount) {
         OptionDefinition def = new OptionDefinition(uniquekey, sLongOption, sShortOption, sHelpText, argtype, maxcount);
         OptionDefinition old = ExistDefinition(def);
         if (old != null)
            throw new Exception(string.Format("Die Option '{0}' steht im Konflikt mit der Option '{1}'.", def.ToString(), old.ToString()));
         else {
            Match ma;
            if (def.LongName().Length > 0) {
               ma = rex_opt_long.Match(def.LongName());
               if (!ma.Success)
                  throw new ArgumentException(string.Format("Der lange Name für die Option '{0}' ist ungültig.", def));
            }
            if (def.ShortName().Length > 0) {
               ma = rex_opt_short.Match(def.ShortName());
               if (!ma.Success)
                  throw new ArgumentException(string.Format("Der kurze Name für die Option '{0}' ist ungültig.", def));
            }
            DefinedOptions.Add(def.iKey, def);
         }
      }

      /// <summary>
      /// existiert diese Definition schon in der Liste (Key, langer oder kurzer Name)
      /// </summary>
      /// <param name="def"></param>
      /// <returns>schon existierende Definition</returns>
      protected OptionDefinition ExistDefinition(OptionDefinition def) {
         if (DefinedOptions.ContainsKey(def.iKey))
            return DefinedOptions[def.iKey];
         foreach (KeyValuePair<int, OptionDefinition> keyvalue in DefinedOptions) {
            OptionDefinition old = keyvalue.Value;
            if ((def.sLongOption.Length > 0 && old.sLongOption == def.sLongOption) ||
                (def.sShortOption.Length > 0 && old.sShortOption == def.sShortOption))
               return old;
         }
         return null;
      }

      /// <summary>
      /// Ist diese Option erlaubt?
      /// </summary>
      /// <param name="sOption">Optionstext</param>
      /// <param name="bShort">kurze oder lange Version</param>
      /// <returns>OptionDefinition, wenn erlaubt</returns>
      protected OptionDefinition IsAllowedOption(string sOption, bool bShort) {
         foreach (KeyValuePair<int, OptionDefinition> keyvalue in DefinedOptions)
            if ((bShort ? keyvalue.Value.sShortOption : keyvalue.Value.sLongOption) == sOption)
               return keyvalue.Value;
         return null;
      }

      /// <summary>
      /// Ist diese Option erlaubt?
      /// </summary>
      /// <param name="sOption">Optionstext</param>
      /// <param name="bShort">kurze oder lange Version</param>
      /// <param name="argtype">Definitionstyp</param>
      /// <returns>Definition, wenn erlaubt, sonst null</returns>
      protected OptionDefinition IsAllowedOption(string sOption, bool bShort, OptionArgumentType argtype) {
         foreach (KeyValuePair<int, OptionDefinition> keyvalue in DefinedOptions) {
            OptionDefinition def = keyvalue.Value;
            if ((bShort ? def.sShortOption : def.sLongOption) == sOption &&
                def.Typ == argtype)
               return def;
         }
         return null;
      }

      protected OptionDefinition IsAllowedOption(int key) {
         return DefinedOptions.ContainsKey(key) ? DefinedOptions[key] : null;
      }

      #endregion

      #region Einlesen und Interpretieren der Kommandozeile

      protected void ClearParse() {
         Parameters = new List<string>();
         SampledOptions = new List<SampledOption>();
      }

      /// <summary>
      /// die Kommandozeile wird eingelesen (kann mehrfach verwendet werden)
      /// </summary>
      /// <param name="sArgs"></param>
      public void Parse(string[] sArgs) {
         ClearParse();
         for (int i = 0; i < sArgs.Length; i++) {
            Match ma = rex_opt_long.Match(sArgs[i]);
            if (ma.Success) {                            // eine lange Option --xxx
               //CaptureCollection cc = ma.Captures;
               string sOptionPrefix = ma.Groups[1].Value;
               string sOption = ma.Groups[2].Value;
               if (sOption == sArgs[i].Substring(sOptionPrefix.Length)) {
                  if (RegisterOptionWithArgument(sOption, false, i < sArgs.Length - 1 ? sArgs[i + 1] : null))
                     i++;
               } else {       // Anfang ist mit Option identisch
                  if (sArgs[i][sOption.Length + sOptionPrefix.Length] == '=' ||
                      sArgs[i][sOption.Length + sOptionPrefix.Length] == ':') {
                     string tmp = sArgs[i].Substring(sOption.Length + sOptionPrefix.Length + 1);
                     RegisterOptionWithArgument(sOption, false, tmp);
                  }
               }
               continue;
            }

            ma = rex_opt_short.Match(sArgs[i]);
            if (ma.Success) {                         // eine kurze Option -x (oder -xyz)
               string sOption = ma.Groups[1].Value;
               if (sOption.Length == 1) {          // eine einzelne Option
                  if (RegisterOptionWithArgument(sOption, true, i < sArgs.Length - 1 ? sArgs[i + 1] : null))
                     i++;
               } else {                            // mehrere zusammengefaßte Optionen
                  for (int j = 0; j < sOption.Length; j++)
                     RegisterOptionWithArgument(sOption.Substring(j, 1), true, null);
               }
            } else {             // dann ist es nur ein Parameter
               Parameters.Add(sArgs[i]);
            }
         }
      }

      /// <summary>
      /// die Option wird (ev. mit ihrem Argument) eingelesen, falls sie (noch) erlaubt ist
      /// </summary>
      /// <param name="sOption"></param>
      /// <param name="oArgument"></param>
      /// <returns>true, falls das Argument dazugehört</returns>
      protected bool RegisterOptionWithArgument(string sOption, bool bShort, string sArgument) {
         bool bWithArgument = false;
         if (IsAllowedOption(sOption, bShort) == null)
            throw new Exception(string.Format("Die Option '{0}{1}' ist nicht erlaubt.", bShort ? "-" : "--", sOption));
         //if (sArgument != null && sArgument.Length > 0) {       // auch kein "leeres" Argument
         if (sArgument != null) {                                 // Test, ob es eine erlaubte Option mit Argument ist
            if (RegisterOption(sOption, bShort, OptionArgumentType.Double, sArgument))
               bWithArgument = true;
            else
               if (RegisterOption(sOption, bShort, OptionArgumentType.Integer, sArgument))
                  bWithArgument = true;
               else
                  if (RegisterOption(sOption, bShort, OptionArgumentType.UnsignedInteger, sArgument))
                     bWithArgument = true;
                  else
                     if (RegisterOption(sOption, bShort, OptionArgumentType.PositivInteger, sArgument))
                        bWithArgument = true;
                     else
                        if (RegisterOption(sOption, bShort, OptionArgumentType.Long, sArgument))
                           bWithArgument = true;
                        else
                           if (RegisterOption(sOption, bShort, OptionArgumentType.UnsignedLong, sArgument))
                              bWithArgument = true;
                           else
                              if (RegisterOption(sOption, bShort, OptionArgumentType.PositivLong, sArgument))
                                 bWithArgument = true;
                              else
                                 if (RegisterOption(sOption, bShort, OptionArgumentType.Boolean, sArgument))
                                    bWithArgument = true;
                                 else
                                    if (RegisterOption(sOption, bShort, OptionArgumentType.String, sArgument))
                                       bWithArgument = true;
                                    else
                                       if (RegisterOption(sOption, bShort, OptionArgumentType.Nothing, null))
                                          bWithArgument = false;
                                       else {
                                          throw new Exception(string.Format("Die Option '{0}{1}' ist ohne Argument nicht erlaubt oder das Argument ist ungültig.", bShort ? "-" : "--", sOption));
                                       }
         } else
            if (RegisterOption(sOption, bShort, OptionArgumentType.Nothing, null))
               bWithArgument = false;
            else {
               throw new Exception(string.Format("Die Option '{0}{1}' ist ohne Argument nicht erlaubt.", bShort ? "-" : "--", sOption));
            }
         return bWithArgument;
      }

      /// <summary>
      /// wenn sie erlaubt ist, wird die Option (ev. mit Argument) registriert; falls sie zu oft verwendet wird, wird eine Exception ausgelöst
      /// </summary>
      /// <param name="sOption"></param>
      /// <param name="bShort"></param>
      /// <param name="argtype"></param>
      /// <param name="sArgument"></param>
      /// <returns></returns>
      protected bool RegisterOption(string sOption, bool bShort, OptionArgumentType argtype, string sArgument) {
         bool ok = false;
         OptionDefinition opt = IsAllowedOption(sOption, bShort, argtype);
         if (opt != null) {
            // erstmal alle ';' als Trennstelle ansehen
            string[] sArgumentList = sArgument != null ? sArgument.Split(new char[] { ';' }) : new string[0];
            // aber: '\;' ist KEINE Trennstelle (ist ein '...\;...' o.ä. ttatsächlich als Trennstelle gedacht, MUSS der Umweg über 2 getrennte Optionsangaben 
            // gegangen werden)
            if (sArgumentList.Length >= 2) {
               List<string> sTmp = new List<string>();
               sTmp.AddRange(sArgumentList);
               for (int i = 0; i < sTmp.Count - 1; i++)
                  if (sTmp[i].Length > 0 && sTmp[i][sTmp[i].Length - 1] == '\\') {     // letztes Zeichen ein '\' --> "maskiertes" ';'
                     sTmp[i] = sTmp[i].Substring(0, sTmp[i].Length - 1);
                     sTmp[i] += ";";
                     sTmp[i] += sTmp[i + 1];
                     sTmp.RemoveAt(i + 1);
                     i--;
                  }
               sArgumentList = new string[sTmp.Count];
               sTmp.CopyTo(sArgumentList);
            }

            int iArg;
            double dArg;
            uint uArg;
            bool bArg;
            // alle Argumente auf Typ testen
            for (int i = 0; i < sArgumentList.Length; i++) {
               switch (argtype) {
                  case OptionArgumentType.Double: if (!SampledOption.DoubleIsPossible(sArgumentList[i], out dArg)) return false; break;
                  case OptionArgumentType.Integer: if (!SampledOption.IntegerIsPossible(sArgumentList[i], out iArg)) return false; break;
                  case OptionArgumentType.UnsignedInteger: if (!SampledOption.UnsignedIntegerIsPossible(sArgumentList[i], out uArg)) return false; break;
                  case OptionArgumentType.PositivInteger: if (!SampledOption.UnsignedIntegerIsPossible(sArgumentList[i], out uArg) || uArg == 0) return false; break;
                  case OptionArgumentType.Boolean: if (!SampledOption.BooleanIsPossible(sArgumentList[i], out bArg)) return false; break;
               }
            }
            // alle Argumente mit der Option registrieren
            for (int i = 0; i < sArgumentList.Length || sArgument == null; i++)
               if (GetSampledOptionPosition(opt.iKey, opt.iMaxCount - 1) < 0) {
                  SampledOptions.Add(new SampledOption(opt.iKey, sOption, bShort, argtype, sArgument == null ? null : sArgumentList[i]));
                  ok = true;
                  if (sArgument == null) break;
               } else
                  throw new Exception(string.Format("Die Option '{0}' darf höchstens {1}mal verwendet werden.", opt.Name(), opt.iMaxCount));
         }
         return ok;
      }

      /// <summary>
      /// liefert die Position der schon eingesammelten Option in der Liste (oder -1)
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="no">no-tes Auftreten</param>
      /// <returns>Index</returns>
      protected int GetSampledOptionPosition(int key, int no) {
         int count = 0;
         for (int i = 0; i < SampledOptions.Count; i++)
            if (SampledOptions[i].iKey == key) {
               if (count == no)
                  return i;
               count++;
            }
         return -1;
      }

      #endregion

      #region Abfrage von Optionen und Parametern

      /// <summary>
      /// Liste der reinen Parameter
      /// </summary>
      public List<string> Parameters { get; private set; }

      /// <summary>
      /// Anzahl der erlaubten Optionen
      /// </summary>
      public int DefinedOptionsCount { get { return DefinedOptions.Count; } }

      /// <summary>
      /// prüft ob der Optionsschlüssel gültig ist und löst andernfalls eine Exception aus
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      protected void CheckValidOption(int key) {
         if (IsAllowedOption(key) == null)
            throw new Exception(string.Format("Der Options-Schlüssel ({0}) existiert nicht.", key));
      }

      /// <summary>
      /// liefert den definierten Typ dieser Option
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <returns></returns>
      public OptionArgumentType OptionType(int key) {
         CheckValidOption(key);
         return DefinedOptions[key].Typ;
      }

      /// <summary>
      /// liefert den Namen dieser Option
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <returns></returns>
      public string OptionName(int key) {
         CheckValidOption(key);
         return DefinedOptions[key].Name();
      }

      /// <summary>
      /// liefert, wie oft diese Option in der Kommandozeile angewendet wurde
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <returns></returns>
      public int OptionAssignment(int key) {
         CheckValidOption(key);
         int count = 0;
         for (int i = 0; i < SampledOptions.Count; i++)
            if (SampledOptions[i].iKey == key)
               count++;
         return count;
      }

      public bool UsedValue(int key) {
         return UsedValue(key, true);
      }
      /// <summary>
      /// liefert, ob die Option verwendet wurde
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="lazy">Argument ignorieren</param>
      /// <returns></returns>
      public bool UsedValue(int key, bool lazy) {
         SampledOption opt = GetSampledOption(key, 0);
         return opt.AsUsed(lazy);
      }

      public int IntegerValue(int key) {
         return IntegerValue(key, 0, true);
      }
      public int IntegerValue(int key, int no) {
         return IntegerValue(key, no, true);
      }
      /// <summary>
      /// liefert das Argument der Option als Integer
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="no">Nummer des Auftretens dieser Option (kleiner als OptionAssignment()!)</param>
      /// <param name="lazy">Argument wenn möglich interpretieren</param>
      /// <returns></returns>
      public int IntegerValue(int key, int no, bool lazy) {
         SampledOption opt = GetSampledOption(key, no);
         return opt.AsInteger(lazy);
      }

      public long LongValue(int key) {
         return LongValue(key, 0, true);
      }
      public long LongValue(int key, int no) {
         return LongValue(key, no, true);
      }
      /// <summary>
      /// liefert das Argument der Option als Long
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="no">Nummer des Auftretens dieser Option (kleiner als OptionAssignment()!)</param>
      /// <param name="lazy">Argument wenn möglich interpretieren</param>
      /// <returns></returns>
      public long LongValue(int key, int no, bool lazy) {
         SampledOption opt = GetSampledOption(key, no);
         return opt.AsLong(lazy);
      }

      public double DoubleValue(int key) {
         return DoubleValue(key, 0, true);
      }
      public double DoubleValue(int key, int no) {
         return DoubleValue(key, no, true);
      }
      /// <summary>
      /// liefert das Argument der Option als Double
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="no">Nummer des Auftretens dieser Option (kleiner als OptionAssignment()!)</param>
      /// <param name="lazy">Argument wenn möglich interpretieren</param>
      /// <returns></returns>
      public double DoubleValue(int key, int no, bool lazy) {
         SampledOption opt = GetSampledOption(key, no);
         return opt.AsDouble(lazy);
      }

      public string StringValue(int key) {
         return StringValue(key, 0);
      }
      /// <summary>
      /// liefert das Argument der Option als String
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="no">Nummer des Auftretens dieser Option (kleiner als OptionAssignment()!)</param>
      /// <returns></returns>
      public string StringValue(int key, int no) {
         SampledOption opt = GetSampledOption(key, no);
         return opt.AsString();
      }

      public uint UnsignedIntegerValue(int key) {
         return UnsignedIntegerValue(key, 0, true);
      }
      public uint UnsignedIntegerValue(int key, int no) {
         return UnsignedIntegerValue(key, no, true);
      }
      /// <summary>
      /// liefert das Argument der Option als UnsignedInteger
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="no">Nummer des Auftretens dieser Option (kleiner als OptionAssignment()!)</param>
      /// <param name="lazy">Argument wenn möglich interpretieren</param>
      /// <returns></returns>
      public uint UnsignedIntegerValue(int key, int no, bool lazy) {
         SampledOption opt = GetSampledOption(key, no);
         return opt.AsUnsignedInteger(lazy);
      }

      public ulong UnsignedLongValue(int key) {
         return UnsignedLongValue(key, 0, true);
      }
      public ulong UnsignedLongValue(int key, int no) {
         return UnsignedLongValue(key, no, true);
      }
      /// <summary>
      /// liefert das Argument der Option als UnsignedLong
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="no">Nummer des Auftretens dieser Option (kleiner als OptionAssignment()!)</param>
      /// <param name="lazy">Argument wenn möglich interpretieren</param>
      /// <returns></returns>
      public ulong UnsignedLongValue(int key, int no, bool lazy) {
         SampledOption opt = GetSampledOption(key, no);
         return opt.AsUnsignedLong(lazy);
      }

      public uint PositivIntegerValue(int key) {
         return PositivIntegerValue(key, 0, true);
      }
      public uint PositivIntegerValue(int key, int no) {
         return PositivIntegerValue(key, no, true);
      }
      /// <summary>
      /// liefert das Argument der Option als PositivInteger
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="no">Nummer des Auftretens dieser Option (kleiner als OptionAssignment()!)</param>
      /// <param name="lazy">Argument wenn möglich interpretieren</param>
      /// <returns></returns>
      public uint PositivIntegerValue(int key, int no, bool lazy) {
         SampledOption opt = GetSampledOption(key, no);
         return opt.AsPositivInteger(lazy);
      }

      public ulong PositivLongValue(int key) {
         return PositivLongValue(key, 0, true);
      }
      public ulong PositivLongValue(int key, int no) {
         return PositivLongValue(key, no, true);
      }
      /// <summary>
      /// liefert das Argument der Option als PositivInteger
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="no">Nummer des Auftretens dieser Option (kleiner als OptionAssignment()!)</param>
      /// <param name="lazy">Argument wenn möglich interpretieren</param>
      /// <returns></returns>
      public ulong PositivLongValue(int key, int no, bool lazy) {
         SampledOption opt = GetSampledOption(key, no);
         return opt.AsPositivLong(lazy);
      }

      public bool BooleanValue(int key) {
         return BooleanValue(key, 0, true);
      }
      public bool BooleanValue(int key, int no) {
         return BooleanValue(key, no, true);
      }
      /// <summary>
      /// liefert das Argument der Option als Boolean
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="no">Nummer des Auftretens dieser Option (kleiner als OptionAssignment()!)</param>
      /// <param name="lazy">Argument wenn möglich interpretieren</param>
      /// <returns></returns>
      public bool BooleanValue(int key, int no, bool lazy) {
         SampledOption opt = GetSampledOption(key, no);
         return opt.AsBoolean(lazy);
      }



      /// <summary>
      /// liefert die eingesammelte Option oder löst eine Exception aus
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="no">Nummer des Auftretens dieser Option (kleiner als OptionAssignment()!)</param>
      /// <returns></returns>
      protected SampledOption GetSampledOption(int key, int no) {
         CheckValidOption(key);
         int count = 0;
         for (int i = 0; i < SampledOptions.Count; i++)
            if (SampledOptions[i].iKey == key) {
               if (count == no)
                  return SampledOptions[i];
               count++;
            }
         return null;
      }

      #endregion


      const int MINGAP_HELPTEXT = 3;

      /// <summary>
      /// liefert je Option die Hilfezeile
      /// </summary>
      /// <returns></returns>
      public List<string> GetHelpText() {
         int[] optkey = new int[DefinedOptionsCount];
         DefinedOptions.Keys.CopyTo(optkey, 0);             // alle definierten Schlüssel holen
         Array.Sort<int>(optkey);                           // ... und sortieren
         List<string> txt = new List<string>();
         int iOptArgLength = 0;
         for (int i = 0; i < optkey.Length; i++) {
            txt.Add(string.Format("{0}{1}", DefinedOptions[optkey[i]].Name(), DefinedOptions[optkey[i]].Typ != OptionArgumentType.Nothing ? "=arg" : ""));
            iOptArgLength = Math.Max(iOptArgLength, txt[i].Length);
         }
         iOptArgLength += MINGAP_HELPTEXT;
         for (int i = 0; i < txt.Count; i++) {
            txt[i] += new string(' ', iOptArgLength - txt[i].Length);
            txt[i] += DefinedOptions[optkey[i]].sHelpText;
         }
         // notfalls noch umbrechen
         for (int i = 0; i < txt.Count; i++) {
            int nl = txt[i].IndexOf('\n');
            if (nl > 0) {        // Zeile trennen
               string newline = new string(' ', iOptArgLength) + txt[i].Substring(nl + 1);
               txt[i] = txt[i].Substring(0, nl);
               txt.Insert(i + 1, newline);
            }
         }
         return txt;
      }

      public override string ToString() {
         return string.Format("{0} definierte Optionen; {1} Optionen erkannt",
                                 DefinedOptions != null ? DefinedOptionsCount : 0,
                                 SampledOptions != null ? SampledOptions.Count : 0);
      }

   }
}
