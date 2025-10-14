using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.MFME
{
    public static class ShortcutKeyHelper
    {
        public static KeyCode GetKeyCode(string shortcutKeyString)
        {
            string shortcutKeyTrimmed = shortcutKeyString.TrimEnd(' ');
            switch (shortcutKeyTrimmed)
            {
                // TODO worth doing escape / f0 - f9, insert, delete?

                case "SPACE":
                    return KeyCode.Space;
                case "1":
                    return KeyCode.Alpha1;
                case "2":
                    return KeyCode.Alpha2;
                case "3":
                    return KeyCode.Alpha3;
                case "4":
                    return KeyCode.Alpha4;
                case "5":
                    return KeyCode.Alpha5;
                case "6":
                    return KeyCode.Alpha6;
                case "7":
                    return KeyCode.Alpha7;
                case "8":
                    return KeyCode.Alpha8;
                case "9":
                    return KeyCode.Alpha9;
                case "0":
                    return KeyCode.Alpha0;
                case "`":
                    return KeyCode.BackQuote;
                case "-":
                    return KeyCode.Minus;
                case "=":
                    return KeyCode.Equals;
                case "A":
                    return KeyCode.A;
                case "B":
                    return KeyCode.B;
                case "C":
                    return KeyCode.C;
                case "D":
                    return KeyCode.D;
                case "E":
                    return KeyCode.E;
                case "F":
                    return KeyCode.F;
                case "G":
                    return KeyCode.G;
                case "H":
                    return KeyCode.H;
                case "I":
                    return KeyCode.I;
                case "J":
                    return KeyCode.J;
                case "K":
                    return KeyCode.K;
                case "L":
                    return KeyCode.L;
                case "M":
                    return KeyCode.M;
                case "N":
                    return KeyCode.N;
                case "O":
                    return KeyCode.O;
                case "P":
                    return KeyCode.P;
                case "Q":
                    return KeyCode.Q;
                case "R":
                    return KeyCode.R;
                case "S":
                    return KeyCode.S;
                case "T":
                    return KeyCode.T;
                case "U":
                    return KeyCode.U;
                case "V":
                    return KeyCode.V;
                case "W":
                    return KeyCode.W;
                case "X":
                    return KeyCode.X;
                case "Y":
                    return KeyCode.Y;
                case "Z":
                    return KeyCode.Z;
                case "[":
                    return KeyCode.LeftBracket;
                case "]":
                    return KeyCode.RightBracket;
                case ";":
                    return KeyCode.Semicolon;
                case "'":
                    return KeyCode.Quote;
                case "#":
                    return KeyCode.Hash;
                case "SHIFT":
                    return KeyCode.LeftShift;
                case "\\":
                    return KeyCode.Backslash;
                case ",":
                    return KeyCode.Comma;
                case ".":
                    return KeyCode.Period;
                case "/":
                    return KeyCode.Slash;
                case "CTRL":
                    return KeyCode.LeftControl;
                case "ALT":
                    return KeyCode.LeftAlt;
                case "UP":
                    return KeyCode.UpArrow;
                case "DOWN":
                    return KeyCode.DownArrow;
                case "LEFT":
                    return KeyCode.LeftArrow;
                case "RIGHT":
                    return KeyCode.RightArrow;
                default:
                    return KeyCode.None;
            }
        }
    }
}
