// SPDX-License-Identifier: MPL-2.0
//
// ManuEngine — a fast, GameObject-free model of the Manu et Mente rules plus the AI search.
//
// This file deliberately has NO UnityEngine dependency so that the exact same code runs:
//   * inside Unity (AIAgent searches on a background thread), and
//   * headless outside Unity (ManuTrainer self-play training, compiled with mono/dotnet).
//
// The rules mirror Chessman.cs / MovePlate.cs / game.cs line for line (move generation,
// enabler gating, en passant, ring rotations, and the victory checks in game.CheckVictory).
// If you change a rule there, change it here too — AIAgent logs a warning in the editor if the
// engine's legal moves ever disagree with the move plates the real game spawns.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace ManuAI
{
    // ---------------------------------------------------------------------------------------
    // Board geometry & static lookup tables
    // ---------------------------------------------------------------------------------------
    public static class Geo
    {
        public static readonly int[] RingSize = { 48, 48, 24, 24, 12, 12, 4 }; // rings 1..7 (A a B b C c D)
        public static readonly int[] RingOffset = { 0, 48, 96, 120, 144, 156, 168 };
        public const int NumCells = 172;

        public static readonly int[] RingOf = new int[NumCells];   // 1..7
        public static readonly int[] PlaceOf = new int[NumCells];  // 1..size
        public static readonly int[] PairOf = new int[8];          // ring -> pair level 1..4

        // Radial neighbours (MapIndex results) as cell indices.
        public static readonly int[][] Inward = new int[NumCells][];
        public static readonly int[][] Outward = new int[NumCells][];
        // Horizontal neighbours: [cell, 0] = place+1, [cell, 1] = place-1 (wrapped).
        public static readonly int[,] Horiz = new int[NumCells, 2];
        // Diagonal single step: Diag[cell, d, dir]; d: 0 => drift +1 (ACW), 1 => drift -1 (CW);
        // dir: 0 => "in", 1 => "out". -1 if no such diagonal.
        public static readonly int[,,] Diag = new int[NumCells, 2, 2];

        public static int Cell(int ring, int place) { return RingOffset[ring - 1] + place - 1; }

        static Geo()
        {
            for (int r = 1; r <= 7; r++)
                for (int p = 1; p <= RingSize[r - 1]; p++)
                {
                    int c = Cell(r, p);
                    RingOf[c] = r; PlaceOf[c] = p;
                }
            for (int r = 1; r <= 7; r++) PairOf[r] = r <= 2 ? 1 : r <= 4 ? 2 : r <= 6 ? 3 : 4;

            for (int c = 0; c < NumCells; c++)
            {
                int r = RingOf[c], p = PlaceOf[c], size = RingSize[r - 1];
                Horiz[c, 0] = Cell(r, Wrap(p + 1, size));
                Horiz[c, 1] = Cell(r, Wrap(p - 1, size));

                Inward[c] = r < 7 ? ToCells(r + 1, MapIndex(r, p, r + 1)) : new int[0];
                Outward[c] = r > 1 ? ToCells(r - 1, MapIndex(r, p, r - 1)) : new int[0];

                for (int d = 0; d < 2; d++)
                    for (int dir = 0; dir < 2; dir++)
                        Diag[c, d, dir] = DiagonalStep(r, p, d == 0 ? 1 : -1, dir == 0);
            }
        }

        static int[] ToCells(int ring, int[] places)
        {
            int[] res = new int[places.Length];
            for (int i = 0; i < places.Length; i++) res[i] = Cell(ring, places[i]);
            return res;
        }

        public static int Wrap(int place, int size) { return ((place - 1) % size + size) % size + 1; }

        // Exact port of Chessman.MapIndex.
        public static int[] MapIndex(int fromRing, int fromPlace, int toRing)
        {
            int toRingSize = RingSize[toRing - 1];
            if ((fromRing == 1 && toRing == 2) || (fromRing == 2 && toRing == 1)) return new[] { fromPlace };
            if ((fromRing == 3 && toRing == 4) || (fromRing == 4 && toRing == 3)) return new[] { fromPlace };
            if ((fromRing == 5 && toRing == 6) || (fromRing == 6 && toRing == 5)) return new[] { fromPlace };
            if ((fromRing == 2 && toRing == 3) || (fromRing == 4 && toRing == 5))
            {
                int m = (fromPlace + 1) / 2; if (m < 1) m = 1; if (m > toRingSize) m = toRingSize;
                return new[] { m };
            }
            if ((fromRing == 3 && toRing == 2) || (fromRing == 5 && toRing == 4))
            {
                int a = fromPlace * 2 - 1, b = fromPlace * 2;
                if (a < 1) a = 1; if (b > toRingSize) b = toRingSize;
                return new[] { a, b };
            }
            if (fromRing == 6 && toRing == 7)
            {
                int m = (fromPlace + 2) / 3; if (m < 1) m = 1; if (m > toRingSize) m = toRingSize;
                return new[] { m };
            }
            if (fromRing == 7 && toRing == 6)
            {
                int a = fromPlace * 3 - 2, b = fromPlace * 3 - 1, c = fromPlace * 3;
                if (a < 1) a = 1; if (c > toRingSize) c = toRingSize;
                return new[] { a, b, c };
            }
            return new[] { fromPlace };
        }

        // Exact port of Chessman's diagonal lookup tables (DiagonalExists).
        static bool DiagonalExists(int ring, int place, int drift, bool inward)
        {
            bool acw = drift > 0;
            bool even = place % 2 == 0;
            if (inward)
            {
                switch (ring)
                {
                    case 1: return true;
                    case 2: return acw ? even : !even;
                    case 3: return true;
                    case 4: return acw ? even : !even;
                    case 5: return true;
                    case 6: return acw ? (place % 3 == 0) : (place % 3 == 1);
                    default: return false;
                }
            }
            return ring >= 2; // every outward diagonal exists except from ring 1
        }

        // Exact port of Chessman.DiagonalStep. Returns a cell index or -1.
        static int DiagonalStep(int fromRing, int fromPlace, int drift, bool inward)
        {
            int nextRing = inward ? fromRing + 1 : fromRing - 1;
            if (nextRing < 1 || nextRing > 7) return -1;
            if (!DiagonalExists(fromRing, fromPlace, drift, inward)) return -1;
            int[] bases = MapIndex(fromRing, fromPlace, nextRing);
            int basePlace = bases.Length > 1 ? (drift > 0 ? bases[bases.Length - 1] : bases[0]) : bases[0];
            return Cell(nextRing, Wrap(basePlace + drift, RingSize[nextRing - 1]));
        }

        // Exact port of Chessman.IsAdjacentCell (including its quirks: no wrap on the +-1 check).
        public static bool IsAdjacentCell(int ring1, int place1, int ring2, int place2)
        {
            if (ring1 == ring2)
            {
                int size = RingSize[ring1 - 1];
                int diff = Math.Abs(place1 - place2);
                return diff == 1 || diff == size - 1;
            }
            if (Math.Abs(ring1 - ring2) == 1)
            {
                int outerRing = Math.Max(ring1, ring2), innerRing = Math.Min(ring1, ring2);
                int outerPlace = ring1 > ring2 ? place1 : place2;
                int innerPlace = ring1 < ring2 ? place1 : place2;
                foreach (int m in MapIndex(outerRing, outerPlace, innerRing))
                    if (m == innerPlace || m == innerPlace + 1 || m == innerPlace - 1) return true;
            }
            return false;
        }

        // Exact port of Chessman.IsOnDiagonalRay (ignores blockers and enablers, max 10 steps).
        public static bool IsOnDiagonalRay(int fromCell, int toCell)
        {
            for (int d = 0; d < 2; d++)
                for (int dir = 0; dir < 2; dir++)
                {
                    int cur = fromCell;
                    for (int step = 0; step < 10; step++)
                    {
                        int next = Diag[cur, d, dir];
                        if (next < 0) break;
                        if (next == toCell) return true;
                        cur = next;
                    }
                }
            return false;
        }
    }

    // ---------------------------------------------------------------------------------------
    // Pieces & moves
    // ---------------------------------------------------------------------------------------
    public static class Pc
    {
        public const int Empty = 0, P = 1, A = 2, T = 3, S = 4, W = 5, H = 6, B = 7;
        public const int Orange = 0, Green = 1;
        public static int Type(int code) { return code & 7; }
        public static int Color(int code) { return code >> 3; }
        public static int Make(int type, int color) { return type | (color << 3); }
        public static bool IsCommander(int type) { return type == H || type == B; }

        public static int FromLetter(string s)
        {
            switch (s)
            {
                case "P": return P; case "A": return A; case "T": return T; case "S": return S;
                case "W": return W; case "H": return H; case "B": return B;
            }
            return Empty;
        }
        public static string Letter(int type) { return " PATSWHB".Substring(type, 1); }
        public static int ColorFromName(string player) { return player == "green" ? Green : Orange; }
        public static string ColorName(int c) { return c == Green ? "green" : "orange"; }
    }

    public enum MoveKind : byte { Quiet = 0, Capture = 1, EnPassant = 2, Rotation = 3 }

    public struct Move
    {
        public short From, To;     // cells (for rotation: From = cell of the rotating S/W, To = -1)
        public MoveKind Kind;
        public byte Pair;          // rotation: 1..4
        public bool Clockwise;     // rotation direction

        public bool IsRotation { get { return Kind == MoveKind.Rotation; } }
        public bool IsCapture { get { return Kind == MoveKind.Capture || Kind == MoveKind.EnPassant; } }
        public bool SameAs(Move o)
        {
            if (Kind == MoveKind.Rotation || o.Kind == MoveKind.Rotation)
                return Kind == o.Kind && Pair == o.Pair && Clockwise == o.Clockwise;
            return From == o.From && To == o.To;
        }
        public static readonly Move None = new Move { From = -1, To = -1 };
        public bool IsNone { get { return From < 0 && Kind != MoveKind.Rotation; } }

        public override string ToString()
        {
            if (IsRotation) return "rot" + Pair + (Clockwise ? "CW" : "ACW");
            return Geo.RingOf[From] + "," + Geo.PlaceOf[From] + (IsCapture ? "x" : "-") + Geo.RingOf[To] + "," + Geo.PlaceOf[To];
        }
    }

    public enum Victory { None = 0, CommandersTaken, HandAndMindInD, HandPlus3PawnsInD, MindPlus3PawnsInD, CommanderPlus4PawnsCc }

    public struct Undo
    {
        public int CapturedCode, CapturedCell;
        public int LmColor, LmFrom, LmTo, LmType;
        public int Winner; public Victory WinType;
        public ulong Hash;
        public int RotStreak;
    }

    // ---------------------------------------------------------------------------------------
    // Board state + rules
    // ---------------------------------------------------------------------------------------
    public sealed class Board
    {
        public readonly byte[] Sq = new byte[Geo.NumCells];
        public int Side;                  // colour to move
        public int LmColor = -1, LmFrom = -1, LmTo = -1, LmType = 0;   // game.lastMove (cells)
        public int Winner = -1;           // -1 = game not over
        // Rotations in a row (game.consecutiveRotations). At MaxRotStreak the side to move may not
        // rotate; if it then has no piece move, the game is drawn (GenerateMoves returns nothing).
        public const int MaxRotStreak = 3;
        public int RotStreak;
        public Victory WinType = Victory.None;
        public ulong Hash;
        public int Ply;

        // ---- Zobrist ----
        static readonly ulong[,] ZPiece = new ulong[16, Geo.NumCells];
        static readonly ulong[] ZLmFrom = new ulong[Geo.NumCells], ZLmTo = new ulong[Geo.NumCells];
        static readonly ulong ZSide;
        static readonly ulong[] ZRot = new ulong[MaxRotStreak + 1];
        static Board()
        {
            var rng = new Random(0x5EED);
            Func<ulong> r64 = () => { var b = new byte[8]; rng.NextBytes(b); return BitConverter.ToUInt64(b, 0); };
            for (int i = 0; i < 16; i++) for (int c = 0; c < Geo.NumCells; c++) ZPiece[i, c] = r64();
            for (int c = 0; c < Geo.NumCells; c++) { ZLmFrom[c] = r64(); ZLmTo[c] = r64(); }
            ZSide = r64();
            for (int i = 0; i <= MaxRotStreak; i++) ZRot[i] = r64();
        }
        ulong LmKey() { return LmColor < 0 ? 0UL : (ZLmFrom[LmFrom] ^ (ZLmTo[LmTo] * (ulong)(LmColor + 1))); }

        public void RecomputeHash()
        {
            ulong h = 0;
            for (int c = 0; c < Geo.NumCells; c++) if (Sq[c] != 0) h ^= ZPiece[Sq[c], c];
            if (Side == Pc.Green) h ^= ZSide;
            h ^= LmKey();
            h ^= ZRot[Math.Min(RotStreak, MaxRotStreak)];
            Hash = h;
        }

        public static Board Initial()
        {
            var b = new Board();
            // Mirrors game.Start()
            string backRank = "ASWBHTSA";
            for (int i = 0; i < 8; i++)
            {
                b.Sq[Geo.Cell(1, 27 + i)] = (byte)Pc.Make(Pc.FromLetter(backRank.Substring(i, 1)), Pc.Orange);
                b.Sq[Geo.Cell(2, 27 + i)] = (byte)Pc.Make(Pc.P, Pc.Orange);
                b.Sq[Geo.Cell(1, 3 + i)] = (byte)Pc.Make(Pc.FromLetter(backRank.Substring(i, 1)), Pc.Green);
                b.Sq[Geo.Cell(2, 3 + i)] = (byte)Pc.Make(Pc.P, Pc.Green);
            }
            b.Side = Pc.Orange;
            b.RecomputeHash();
            return b;
        }

        public Board Clone()
        {
            var b = new Board();
            Buffer.BlockCopy(Sq, 0, b.Sq, 0, Sq.Length);
            b.Side = Side; b.LmColor = LmColor; b.LmFrom = LmFrom; b.LmTo = LmTo; b.LmType = LmType;
            b.Winner = Winner; b.WinType = WinType; b.Hash = Hash; b.Ply = Ply; b.RotStreak = RotStreak;
            return b;
        }

        // ================= Move generation (mirrors Chessman.InitiateMovePlates) =================

        // Per-piece plate buffer (mirrors the MovePlate objects one piece spawns).
        readonly int[] plTo = new int[64];
        readonly bool[] plAtk = new bool[64];
        int plN;

        void AddPlate(int to, bool attack)
        {
            for (int i = 0; i < plN; i++) if (plTo[i] == to && plAtk[i] == attack) return; // dedupe
            plTo[plN] = to; plAtk[plN] = attack; plN++;
        }
        bool HasNormalPlate(int to)
        {
            for (int i = 0; i < plN; i++) if (plTo[i] == to && !plAtk[i]) return true;
            return false;
        }
        void RemovePlatesAt(int to)
        {
            int w = 0;
            for (int i = 0; i < plN; i++)
                if (plTo[i] != to) { plTo[w] = plTo[i]; plAtk[w] = plAtk[i]; w++; }
            plN = w;
        }

        // Highest pair level (1..4) holding a piece of `type` for `color`, 0 if none.
        int MaxPairLevel(int color, int type)
        {
            int code = Pc.Make(type, color);
            for (int r = 7; r >= 1; r--)
            {
                int off = Geo.RingOffset[r - 1], n = Geo.RingSize[r - 1];
                for (int i = 0; i < n; i++) if (Sq[off + i] == code) return Geo.PairOf[r];
            }
            return 0;
        }

        // Generates all legal moves for the side to move into `list` (cleared first).
        public void GenerateMoves(List<Move> list)
        {
            list.Clear();
            if (Winner >= 0) return;
            int me = Side;
            int tLevel = MaxPairLevel(me, Pc.T), wLevel = MaxPairLevel(me, Pc.W);
            bool rotSeen1 = false, rotSeen2 = false, rotSeen3 = false, rotSeen4 = false;

            for (int c = 0; c < Geo.NumCells; c++)
            {
                int code = Sq[c];
                if (code == 0 || Pc.Color(code) != me) continue;
                int type = Pc.Type(code);
                plN = 0;
                switch (type)
                {
                    case Pc.P: PawnPlates(c, me); break;
                    case Pc.A: RayPlates(c, me, tLevel, true); break;
                    case Pc.S: RayPlates(c, me, wLevel, false); EnPassantPlate(c, me, type); break;
                    case Pc.T: TechPlates(c, me); break;
                    case Pc.W:
                        StepPlate(c, Geo.Horiz[c, 0]); StepPlate(c, Geo.Horiz[c, 1]);
                        RadialPlates(c, false, me);
                        EnPassantPlate(c, me, type);
                        break;
                    case Pc.H: case Pc.B:
                        StepPlate(c, Geo.Horiz[c, 0]); StepPlate(c, Geo.Horiz[c, 1]);
                        RadialPlates(c, false, me);
                        break;
                }
                for (int i = 0; i < plN; i++)
                {
                    int to = plTo[i];
                    MoveKind k = !plAtk[i] ? MoveKind.Quiet : (Sq[to] != 0 ? MoveKind.Capture : MoveKind.EnPassant);
                    list.Add(new Move { From = (short)c, To = (short)to, Kind = k });
                }
                if ((type == Pc.S || type == Pc.W) && RotStreak < MaxRotStreak)
                {
                    int pair = Geo.PairOf[Geo.RingOf[c]];
                    bool seen = pair == 1 ? rotSeen1 : pair == 2 ? rotSeen2 : pair == 3 ? rotSeen3 : rotSeen4;
                    if (!seen)
                    {
                        if (pair == 1) rotSeen1 = true; else if (pair == 2) rotSeen2 = true; else if (pair == 3) rotSeen3 = true; else rotSeen4 = true;
                        list.Add(new Move { From = (short)c, To = -1, Kind = MoveKind.Rotation, Pair = (byte)pair, Clockwise = false });
                        list.Add(new Move { From = (short)c, To = -1, Kind = MoveKind.Rotation, Pair = (byte)pair, Clockwise = true });
                    }
                }
            }
        }

        void StepPlate(int from, int to) { if (Sq[to] == 0) AddPlate(to, false); }

        void PawnPlates(int c, int me)
        {
            StepPlate(c, Geo.Horiz[c, 0]);
            StepPlate(c, Geo.Horiz[c, 1]);
            foreach (int t in Geo.Inward[c]) StepPlate(c, t);
            foreach (int t in Geo.Outward[c]) StepPlate(c, t);
            for (int d = 0; d < 2; d++)
                for (int dir = 0; dir < 2; dir++)
                {
                    int t = Geo.Diag[c, d, dir];
                    if (t >= 0) StepPlate(c, t);
                }
            EnPassantPlate(c, me, Pc.P);
        }

        void RadialPlates(int c, bool capture, int me)
        {
            foreach (int t in Geo.Inward[c]) { if (capture) CapPlate(t, me); else StepPlate(c, t); }
            foreach (int t in Geo.Outward[c]) { if (capture) CapPlate(t, me); else StepPlate(c, t); }
        }

        void CapPlate(int to, int me)
        {
            int occ = Sq[to];
            if (occ == 0) AddPlate(to, false);
            else if (Pc.Color(occ) != me) AddPlate(to, true);
        }

        void TechPlates(int c, int me)
        {
            CapPlate(Geo.Horiz[c, 0], me);
            CapPlate(Geo.Horiz[c, 1], me);
            RadialPlates(c, true, me);
        }

        // AddDiagonalRays: Apprentice (enabler T, captures) / Scholar (enabler W, no capture).
        void RayPlates(int start, int me, int enablerLevel, bool canCapture)
        {
            for (int d = 0; d < 2; d++)
                for (int dir = 0; dir < 2; dir++)
                {
                    int cur = start;
                    for (int step = 0; step < 10; step++)
                    {
                        int next = Geo.Diag[cur, d, dir];
                        if (next < 0) break;
                        int curPair = Geo.PairOf[Geo.RingOf[cur]], destPair = Geo.PairOf[Geo.RingOf[next]];
                        if (destPair != curPair && enablerLevel < destPair) break; // CanJumpToRing
                        int occ = Sq[next];
                        if (occ == 0) AddPlate(next, false);
                        else
                        {
                            if (Pc.Color(occ) != me && canCapture) AddPlate(next, true);
                            break;
                        }
                        cur = next;
                    }
                }
        }

        // Exact port of Chessman.AddEnPassantCapture (must run after the piece's normal plates).
        void EnPassantPlate(int c, int me, int type)
        {
            if (LmColor < 0 || LmColor == me) return;
            int victim = Sq[LmTo];
            if (victim == 0 || Pc.Color(victim) == me) return;
            int target = LmFrom;
            if (Sq[target] != 0) return;
            if (!ReachableForEnPassant(type, c, target)) return;
            if (!Geo.IsAdjacentCell(Geo.RingOf[target], Geo.PlaceOf[target], Geo.RingOf[LmTo], Geo.PlaceOf[LmTo])) return;
            if (!HasNormalPlate(target)) return;
            RemovePlatesAt(target);
            AddPlate(target, true);
        }

        static bool ReachableForEnPassant(int type, int from, int target)
        {
            int r = Geo.RingOf[from], p = Geo.PlaceOf[from], tr = Geo.RingOf[target], tp = Geo.PlaceOf[target];
            if (type == Pc.P || type == Pc.W)
            {
                if (tr == r)
                {
                    int size = Geo.RingSize[r - 1], diff = Math.Abs(tp - p);
                    if (diff == 1 || diff == size - 1) return true;
                    if (type == Pc.W) return false;
                }
                if (Math.Abs(tr - r) == 1) return Geo.IsAdjacentCell(r, p, tr, tp);
                return false;
            }
            if (type == Pc.S) return Geo.IsOnDiagonalRay(from, target);
            return false;
        }

        // ================= Make / unmake =================

        public void Make(Move m, ref Undo u)
        {
            u.LmColor = LmColor; u.LmFrom = LmFrom; u.LmTo = LmTo; u.LmType = LmType;
            u.Winner = Winner; u.WinType = WinType; u.Hash = Hash;
            u.CapturedCode = 0; u.CapturedCell = -1;
            u.RotStreak = RotStreak;
            Hash ^= ZRot[Math.Min(RotStreak, MaxRotStreak)];
            RotStreak = m.Kind == MoveKind.Rotation ? RotStreak + 1 : 0;
            Hash ^= ZRot[Math.Min(RotStreak, MaxRotStreak)];

            if (m.Kind == MoveKind.Rotation)
            {
                RotatePair(m.Pair, m.Clockwise);
            }
            else
            {
                Hash ^= LmKey();
                int piece = Sq[m.From];
                if (m.Kind == MoveKind.Capture)
                {
                    u.CapturedCode = Sq[m.To]; u.CapturedCell = m.To;
                    Hash ^= ZPiece[Sq[m.To], m.To];
                }
                else if (m.Kind == MoveKind.EnPassant)
                {
                    u.CapturedCode = Sq[LmTo]; u.CapturedCell = LmTo;
                    Hash ^= ZPiece[Sq[LmTo], LmTo];
                    Sq[LmTo] = 0;
                }
                Hash ^= ZPiece[piece, m.From] ^ ZPiece[piece, m.To];
                Sq[m.From] = 0;
                Sq[m.To] = (byte)piece;
                LmColor = Pc.Color(piece); LmFrom = m.From; LmTo = m.To; LmType = Pc.Type(piece);
                Hash ^= LmKey();
            }
            Side ^= 1; Hash ^= ZSide; Ply++;
            CheckVictory();
        }

        public void Unmake(Move m, ref Undo u)
        {
            Side ^= 1; Ply--;
            if (m.Kind == MoveKind.Rotation)
            {
                RotatePair(m.Pair, !m.Clockwise);
            }
            else
            {
                byte piece = Sq[m.To];
                Sq[m.To] = 0;
                Sq[m.From] = piece;
                if (u.CapturedCell >= 0) Sq[u.CapturedCell] = (byte)u.CapturedCode;
            }
            LmColor = u.LmColor; LmFrom = u.LmFrom; LmTo = u.LmTo; LmType = u.LmType;
            Winner = u.Winner; WinType = u.WinType; Hash = u.Hash; RotStreak = u.RotStreak;
        }

        readonly byte[] rotTmp = new byte[48];
        void RotatePair(int pair, bool clockwise)
        {
            int shift, r1, r2;
            switch (pair)
            {
                case 1: r1 = 1; r2 = 2; shift = 12; break;
                case 2: r1 = 3; r2 = 4; shift = 6; break;
                case 3: r1 = 5; r2 = 6; shift = 3; break;
                default: r1 = 7; r2 = 7; shift = 1; break;
            }
            if (clockwise) shift = -shift;
            RotateRing(r1, shift);
            if (r2 != r1) RotateRing(r2, shift);
        }
        void RotateRing(int ring, int shift)
        {
            int off = Geo.RingOffset[ring - 1], size = Geo.RingSize[ring - 1];
            for (int i = 0; i < size; i++)
            {
                byte pc = Sq[off + i];
                if (pc != 0) Hash ^= ZPiece[pc, off + i];
                rotTmp[((i + shift) % size + size) % size] = pc;
            }
            for (int i = 0; i < size; i++)
            {
                Sq[off + i] = rotTmp[i];
                if (rotTmp[i] != 0) Hash ^= ZPiece[rotTmp[i], off + i];
            }
        }

        // ================= Victory (mirrors game.CheckVictory) =================

        public bool HasCommander(int color)
        {
            int h = Pc.Make(Pc.H, color), b = Pc.Make(Pc.B, color);
            for (int c = 0; c < Geo.NumCells; c++) if (Sq[c] == h || Sq[c] == b) return true;
            return false;
        }

        public int CountCommanders(int color)
        {
            int h = Pc.Make(Pc.H, color), b = Pc.Make(Pc.B, color), n = 0;
            for (int c = 0; c < Geo.NumCells; c++) if (Sq[c] == h || Sq[c] == b) n++;
            return n;
        }

        public Victory ClassifyPositional(int color)
        {
            bool handInD = false, mindInD = false; int pawnsInD = 0;
            int off = Geo.RingOffset[6];
            for (int i = 0; i < 4; i++)
            {
                int code = Sq[off + i];
                if (code == 0 || Pc.Color(code) != color) continue;
                int t = Pc.Type(code);
                if (t == Pc.H) handInD = true; else if (t == Pc.B) mindInD = true; else if (t == Pc.P) pawnsInD++;
            }
            if (handInD && mindInD) return Victory.HandAndMindInD;
            if (handInD && pawnsInD >= 3) return Victory.HandPlus3PawnsInD;
            if (mindInD && pawnsInD >= 3) return Victory.MindPlus3PawnsInD;
            // Comeback win: only for a player who has already lost one of their two commanders.
            if ((handInD || mindInD) && CountCommanders(color) == 1)
            {
                int pawnsCc = 0, pawn = Pc.Make(Pc.P, color);
                for (int c = Geo.RingOffset[4]; c < Geo.RingOffset[6]; c++) if (Sq[c] == pawn) pawnsCc++;
                if (pawnsCc >= 4) return Victory.CommanderPlus4PawnsCc;
            }
            return Victory.None;
        }

        public void CheckVictory()
        {
            if (!HasCommander(Pc.Orange)) { Winner = Pc.Green; WinType = Victory.CommandersTaken; return; }
            if (!HasCommander(Pc.Green)) { Winner = Pc.Orange; WinType = Victory.CommandersTaken; return; }
            Victory v = ClassifyPositional(Pc.Orange);
            if (v != Victory.None) { Winner = Pc.Orange; WinType = v; return; }
            v = ClassifyPositional(Pc.Green);
            if (v != Victory.None) { Winner = Pc.Green; WinType = v; return; }
            Winner = -1; WinType = Victory.None;
        }
    }

    // ---------------------------------------------------------------------------------------
    // Evaluation: a linear function of hand-designed features (weights are learned by
    // ManuTrainer via self-play + logistic "Texel" tuning). Every feature is (us - them)
    // from the side-to-move's perspective, so the evaluation is symmetric by construction.
    // ---------------------------------------------------------------------------------------
    public static class Eval
    {
        // Features 0..48: piece-square table, index (type-1)*7 + (ring-1).
        public const int F_PST = 0;
        public const int F_PawnsInD_WithCmd = 49;  // own pawns in D, counted only if own commander is in D
        public const int F_PawnsCc_WithCmd = 50;   // min(own pawns in C/c, 4) if own LAST commander is in D
        public const int F_PawnsCc = 51;           // min(own pawns in C/c, 4)
        public const int F_CmdKnocking = 52;       // own commanders on c whose D target cell is empty
        public const int F_LoneCommander = 53;     // 1 if only one commander left
        public const int F_Tempo = 54;             // constant 1 (side to move)
        public const int F_EnablerT = 55;          // pair level reached by own Technician (gates Apprentices)
        public const int F_EnablerW = 56;          // pair level reached by own Sage (gates Scholars)
        public const int F_PawnsC = 57;            // own pawns in rings 5-7 (pawn support for the centre)
        public const int Count = 58;

        public static void Features(Board b, double[] f)
        {
            Array.Clear(f, 0, Count);
            int us = b.Side;
            int[] cmdInD = new int[2], pawnsInD = new int[2], pawnsCc = new int[2], cmds = new int[2], knock = new int[2];
            int[] tLvl = new int[2], wLvl = new int[2], pawnsInner = new int[2];
            int dOff = Geo.RingOffset[6];
            for (int c = 0; c < Geo.NumCells; c++)
            {
                int code = b.Sq[c];
                if (code == 0) continue;
                int col = Pc.Color(code), t = Pc.Type(code), r = Geo.RingOf[c];
                double sgn = col == us ? 1.0 : -1.0;
                f[F_PST + (t - 1) * 7 + (r - 1)] += sgn;
                if (Pc.IsCommander(t))
                {
                    cmds[col]++;
                    if (r == 7) cmdInD[col]++;
                    else if (r == 6)
                    {
                        int dCell = Geo.Inward[c][0];
                        if (b.Sq[dCell] == 0) knock[col]++;
                    }
                }
                else if (t == Pc.P)
                {
                    if (r == 7) pawnsInD[col]++;
                    else if (r >= 5) pawnsCc[col]++;
                    if (r >= 5) pawnsInner[col]++;
                }
                else if (t == Pc.T) { int lv = Geo.PairOf[r]; if (lv > tLvl[col]) tLvl[col] = lv; }
                else if (t == Pc.W) { int lv = Geo.PairOf[r]; if (lv > wLvl[col]) wLvl[col] = lv; }
            }
            int them = us ^ 1;
            f[F_PawnsInD_WithCmd] = (cmdInD[us] > 0 ? pawnsInD[us] : 0) - (cmdInD[them] > 0 ? pawnsInD[them] : 0);
            f[F_PawnsCc_WithCmd] = (cmdInD[us] > 0 && cmds[us] == 1 ? Math.Min(pawnsCc[us], 4) : 0) - (cmdInD[them] > 0 && cmds[them] == 1 ? Math.Min(pawnsCc[them], 4) : 0);
            f[F_PawnsCc] = Math.Min(pawnsCc[us], 4) - Math.Min(pawnsCc[them], 4);
            f[F_CmdKnocking] = knock[us] - knock[them];
            f[F_LoneCommander] = (cmds[us] == 1 ? 1 : 0) - (cmds[them] == 1 ? 1 : 0);
            f[F_Tempo] = 1;
            f[F_EnablerT] = tLvl[us] - tLvl[them];
            f[F_EnablerW] = wLvl[us] - wLvl[them];
            f[F_PawnsC] = pawnsInner[us] - pawnsInner[them];
        }

        // Hand-written starting point (used before any training and as the tuning prior).
        public static double[] PriorWeights()
        {
            var w = new double[Count];
            double[] baseVal = { 0, 10, 8, 30, 15, 30, 50, 50 };            // by type
            double[] perRing = { 0, 2, 1, 3, 1, 3, 0, 0 };
            double[] cmdRing = { 0, 3, 8, 14, 24, 40, 140 };                 // commander by ring 1..7
            for (int t = 1; t <= 7; t++)
                for (int r = 1; r <= 7; r++)
                    w[F_PST + (t - 1) * 7 + (r - 1)] = Pc.IsCommander(t) ? baseVal[t] + cmdRing[r - 1] : baseVal[t] + perRing[t] * r;
            w[F_PawnsInD_WithCmd] = 60;
            w[F_PawnsCc_WithCmd] = 40;
            w[F_PawnsCc] = 6;
            w[F_CmdKnocking] = 30;
            w[F_LoneCommander] = -25;
            w[F_Tempo] = 5;
            w[F_EnablerT] = 4;
            w[F_EnablerW] = 4;
            w[F_PawnsC] = 3;
            return w;
        }

        // Fast evaluation: same as dot(weights, Features) but without allocating.
        public static double Evaluate(Board b, double[] w)
        {
            int us = b.Side;
            double s = w[F_Tempo];
            int cmdInD0 = 0, cmdInD1 = 0, pD0 = 0, pD1 = 0, pCc0 = 0, pCc1 = 0, cm0 = 0, cm1 = 0, kn0 = 0, kn1 = 0;
            int tl0 = 0, tl1 = 0, wl0 = 0, wl1 = 0, pi0 = 0, pi1 = 0;
            byte[] sq = b.Sq;
            for (int c = 0; c < Geo.NumCells; c++)
            {
                int code = sq[c];
                if (code == 0) continue;
                int col = code >> 3, t = code & 7, r = Geo.RingOf[c];
                double v = w[(t - 1) * 7 + (r - 1)];
                if (col == us) s += v; else s -= v;
                if (t >= Pc.H)
                {
                    if (col == 0) { cm0++; if (r == 7) cmdInD0++; else if (r == 6 && sq[Geo.Inward[c][0]] == 0) kn0++; }
                    else { cm1++; if (r == 7) cmdInD1++; else if (r == 6 && sq[Geo.Inward[c][0]] == 0) kn1++; }
                }
                else if (t == Pc.P)
                {
                    if (col == 0) { if (r == 7) pD0++; else if (r >= 5) pCc0++; if (r >= 5) pi0++; }
                    else { if (r == 7) pD1++; else if (r >= 5) pCc1++; if (r >= 5) pi1++; }
                }
                else if (t == Pc.T) { int lv = Geo.PairOf[r]; if (col == 0) { if (lv > tl0) tl0 = lv; } else if (lv > tl1) tl1 = lv; }
                else if (t == Pc.W) { int lv = Geo.PairOf[r]; if (col == 0) { if (lv > wl0) wl0 = lv; } else if (lv > wl1) wl1 = lv; }
            }
            double fPD = (cmdInD0 > 0 ? pD0 : 0) - (cmdInD1 > 0 ? pD1 : 0);
            double fPCcCmd = (cmdInD0 > 0 && cm0 == 1 ? Math.Min(pCc0, 4) : 0) - (cmdInD1 > 0 && cm1 == 1 ? Math.Min(pCc1, 4) : 0);
            double fPCc = Math.Min(pCc0, 4) - Math.Min(pCc1, 4);
            double fKn = kn0 - kn1;
            double fLone = (cm0 == 1 ? 1 : 0) - (cm1 == 1 ? 1 : 0);
            double fT = tl0 - tl1, fW = wl0 - wl1, fPi = pi0 - pi1;
            double rest = w[F_PawnsInD_WithCmd] * fPD + w[F_PawnsCc_WithCmd] * fPCcCmd + w[F_PawnsCc] * fPCc
                        + w[F_CmdKnocking] * fKn + w[F_LoneCommander] * fLone + w[F_EnablerT] * fT
                        + w[F_EnablerW] * fW + w[F_PawnsC] * fPi;
            return s + (us == 0 ? rest : -rest);
        }

        // The original AIAgent.EvaluateBoard with its default AIWeights, kept only as a benchmark
        // opponent so training progress can be measured against the old evaluation.
        public static double EvaluateLegacy(Board b)
        {
            int ai = b.Side;
            double[] val = { 5, 10, 8, 30, 15, 30, 50, 50 };
            int aiDepth = 0, oppDepth = 0;
            for (int c = 0; c < Geo.NumCells; c++)
            {
                int code = b.Sq[c]; if (code == 0 || !Pc.IsCommander(Pc.Type(code))) continue;
                int r = Geo.RingOf[c];
                if (Pc.Color(code) == ai) { if (r > aiDepth) aiDepth = r; } else if (r > oppDepth) oppDepth = r;
            }
            bool underThreat = oppDepth >= 5, aggressive = aiDepth >= oppDepth;
            double score = 0;
            for (int c = 0; c < Geo.NumCells; c++)
            {
                int code = b.Sq[c]; if (code == 0) continue;
                int t = Pc.Type(code), r = Geo.RingOf[c]; bool cmd = Pc.IsCommander(t); bool mine = Pc.Color(code) == ai;
                double v = val[t];
                if (r == 7) v += cmd ? 100 : 10;
                if (r >= 5 && r < 7) v += 8;
                v += 5 * r;
                if (cmd && !mine) { double prox = 3 * r; if (underThreat && !aggressive) prox *= 2.5; v += prox; }
                if (cmd && mine && underThreat && aggressive) v += 6 * r;
                score += mine ? v : -v;
            }
            return score;
        }
    }

    // ---------------------------------------------------------------------------------------
    // Search: iterative-deepening negamax alpha-beta with a transposition table, killer moves,
    // capture-only quiescence, and a hard time limit. Pure C#, safe to run on a worker thread.
    // ---------------------------------------------------------------------------------------
    public sealed class SearchResult
    {
        public Move Best = Move.None;
        public double Score;
        public int Depth;
        public long Nodes;
        public long Millis;
    }

    public sealed class Searcher
    {
        public const double WinScore = 1000000;
        public double[] Weights;
        public bool UseLegacyEval;
        public int MaxDepth = 4;
        public long TimeLimitMs = 3000;
        public int QuiescenceDepth = 4;
        public double RootNoise;       // random jitter (eval units) added to root move scores
        public Random Rng = new Random();
        public volatile bool CancelRequested;

        struct TTEntry { public ulong Key; public double Score; public short Depth; public byte Flag; public Move Best; }
        const int TTBits = 18;
        readonly TTEntry[] tt = new TTEntry[1 << TTBits];
        readonly Move[,] killers = new Move[64, 2];
        readonly List<Move>[] moveLists = new List<Move>[80];
        readonly double[][] orderScores = new double[80][];
        Stopwatch sw = new Stopwatch();
        long nodes;
        bool aborted;

        public Searcher(double[] weights)
        {
            Weights = weights;
            for (int i = 0; i < moveLists.Length; i++) { moveLists[i] = new List<Move>(128); orderScores[i] = new double[512]; }
        }

        public void ClearTT() { Array.Clear(tt, 0, tt.Length); }

        double Static(Board b) { return UseLegacyEval ? Eval.EvaluateLegacy(b) : Eval.Evaluate(b, Weights); }

        // Score of a finished game from the side-to-move's perspective (prefers faster wins).
        static double TerminalScore(Board b, int ply)
        {
            return b.Winner == b.Side ? WinScore - ply : -WinScore + ply;
        }

        public SearchResult Search(Board root)
        {
            var res = new SearchResult();
            sw.Restart(); nodes = 0; aborted = false; CancelRequested = false;
            Array.Clear(killers, 0, killers.Length);
            Board b = root.Clone();

            var rootMoves = new List<Move>();
            b.GenerateMoves(rootMoves);
            if (rootMoves.Count == 0) return res;
            res.Best = rootMoves[0];
            if (rootMoves.Count == 1) { res.Depth = 0; return res; }

            var scores = new double[rootMoves.Count];
            var noise = new double[rootMoves.Count];
            for (int i = 0; i < noise.Length; i++) noise[i] = RootNoise > 0 ? (Rng.NextDouble() * 2 - 1) * RootNoise : 0;

            for (int depth = 1; depth <= MaxDepth; depth++)
            {
                double alpha = -2 * WinScore, beta = 2 * WinScore;
                int bestIdx = -1; double bestScore = double.NegativeInfinity;
                for (int i = 0; i < rootMoves.Count; i++)
                {
                    Undo u = new Undo();
                    b.Make(rootMoves[i], ref u);
                    double s;
                    if (b.Winner >= 0) s = -TerminalScore(b, 1);
                    else
                    {
                        // Principal-variation search at the root once a best move exists.
                        double a = Math.Max(alpha, bestScore - noise[i] - 1e-6);
                        if (bestIdx >= 0)
                        {
                            s = -Negamax(b, depth - 1, 1, -a - 1, -a);
                            if (s > a && !aborted) s = -Negamax(b, depth - 1, 1, -2 * WinScore, -a);
                        }
                        else s = -Negamax(b, depth - 1, 1, -2 * WinScore, 2 * WinScore);
                    }
                    b.Unmake(rootMoves[i], ref u);
                    if (aborted) break;
                    scores[i] = s;
                    double sn = s + (Math.Abs(s) < WinScore / 2 ? noise[i] : 0);
                    if (sn > bestScore) { bestScore = sn; bestIdx = i; }
                }
                if (aborted && depth > 1) break;  // keep the last fully searched depth
                if (bestIdx >= 0)
                {
                    res.Best = rootMoves[bestIdx]; res.Score = scores[bestIdx]; res.Depth = depth;
                    // Move the best move to the front for the next iteration.
                    Move bm = rootMoves[bestIdx]; rootMoves.RemoveAt(bestIdx); rootMoves.Insert(0, bm);
                    double bn = noise[bestIdx]; for (int k = bestIdx; k > 0; k--) noise[k] = noise[k - 1]; noise[0] = bn;
                    double bs = scores[bestIdx]; for (int k = bestIdx; k > 0; k--) scores[k] = scores[k - 1]; scores[0] = bs;
                }
                if (aborted) break;
                if (Math.Abs(res.Score) > WinScore / 2) break; // forced result found
                if (sw.ElapsedMilliseconds > TimeLimitMs / 2) break; // next depth would not finish
            }
            res.Nodes = nodes; res.Millis = sw.ElapsedMilliseconds;
            return res;
        }

        bool TimeUp()
        {
            if ((nodes & 1023) == 0 && (sw.ElapsedMilliseconds > TimeLimitMs || CancelRequested)) aborted = true;
            return aborted;
        }

        double Negamax(Board b, int depth, int ply, double alpha, double beta)
        {
            nodes++;
            if (TimeUp()) return 0;
            if (b.Winner >= 0) return TerminalScore(b, ply);
            if (depth <= 0 || ply >= 60) return Quiesce(b, ply, alpha, beta, QuiescenceDepth);

            double alphaOrig = alpha;
            int ttIdx = (int)(b.Hash & ((1 << TTBits) - 1));
            Move ttMove = Move.None;
            if (tt[ttIdx].Key == b.Hash)
            {
                TTEntry e = tt[ttIdx];
                ttMove = e.Best;
                if (e.Depth >= depth)
                {
                    if (e.Flag == 0) return e.Score;
                    if (e.Flag == 1 && e.Score >= beta) return e.Score;
                    if (e.Flag == 2 && e.Score <= alpha) return e.Score;
                }
            }

            List<Move> moves = moveLists[ply];
            b.GenerateMoves(moves);
            if (moves.Count == 0) return 0;
            OrderMoves(b, moves, ply, ttMove);

            double best = double.NegativeInfinity; Move bestMove = moves[0];
            for (int i = 0; i < moves.Count; i++)
            {
                Move m = moves[i];
                Undo u = new Undo();
                b.Make(m, ref u);
                double s = -Negamax(b, depth - 1, ply + 1, -beta, -alpha);
                b.Unmake(m, ref u);
                if (aborted) return 0;
                if (s > best) { best = s; bestMove = m; }
                if (s > alpha) alpha = s;
                if (alpha >= beta)
                {
                    if (!m.IsCapture && ply < 64) { killers[ply, 1] = killers[ply, 0]; killers[ply, 0] = m; }
                    break;
                }
            }
            tt[ttIdx] = new TTEntry
            {
                Key = b.Hash, Score = best, Depth = (short)depth, Best = bestMove,
                Flag = (byte)(best <= alphaOrig ? 2 : best >= beta ? 1 : 0)
            };
            return best;
        }

        double Quiesce(Board b, int ply, double alpha, double beta, int qdepth)
        {
            nodes++;
            if (TimeUp()) return 0;
            if (b.Winner >= 0) return TerminalScore(b, ply);
            double stand = Static(b);
            if (qdepth <= 0 || ply >= 70) return stand;
            if (stand >= beta) return stand;
            if (stand > alpha) alpha = stand;

            List<Move> moves = moveLists[ply];
            b.GenerateMoves(moves);
            // Keep captures and moves that immediately win (a commander stepping into D etc.).
            int n = 0;
            for (int i = 0; i < moves.Count; i++)
            {
                Move m = moves[i];
                bool keep = m.IsCapture;
                if (!keep && !m.IsRotation && Geo.RingOf[m.To] == 7) keep = true;
                if (keep) moves[n++] = m;
            }
            moves.RemoveRange(n, moves.Count - n);
            OrderMoves(b, moves, ply, Move.None);
            double best = stand;
            for (int i = 0; i < moves.Count; i++)
            {
                Move m = moves[i];
                Undo u = new Undo();
                b.Make(m, ref u);
                double s = -Quiesce(b, ply + 1, -beta, -alpha, qdepth - 1);
                b.Unmake(m, ref u);
                if (aborted) return 0;
                if (s > best) best = s;
                if (s > alpha) alpha = s;
                if (alpha >= beta) break;
            }
            return best;
        }

        static readonly double[] VictimValue = { 0, 10, 8, 30, 15, 30, 60, 60 };

        void OrderMoves(Board b, List<Move> moves, int ply, Move ttMove)
        {
            double[] sc = orderScores[ply];
            if (sc.Length < moves.Count) { sc = new double[moves.Count * 2]; orderScores[ply] = sc; }
            for (int i = 0; i < moves.Count; i++)
            {
                Move m = moves[i];
                double s = 0;
                if (!ttMove.IsNone && m.SameAs(ttMove)) s = 1e9;
                else if (m.IsCapture)
                {
                    int victim = m.Kind == MoveKind.Capture ? b.Sq[m.To] : b.Sq[b.LmTo];
                    s = 1e6 + VictimValue[Pc.Type(victim)] * 10 - VictimValue[Pc.Type(b.Sq[m.From])];
                }
                else if (ply < 64 && (m.SameAs(killers[ply, 0]) || m.SameAs(killers[ply, 1]))) s = 5e5;
                else if (!m.IsRotation)
                {
                    int t = Pc.Type(b.Sq[m.From]);
                    int dr = Geo.RingOf[m.To] - Geo.RingOf[m.From];
                    s = dr * (Pc.IsCommander(t) ? 30 : t == Pc.P ? 10 : 5);
                    if (Geo.RingOf[m.To] == 7) s += 1000;
                }
                sc[i] = s;
            }
            // insertion sort (lists are small)
            for (int i = 1; i < moves.Count; i++)
            {
                Move m = moves[i]; double s = sc[i]; int j = i - 1;
                while (j >= 0 && sc[j] < s) { moves[j + 1] = moves[j]; sc[j + 1] = sc[j]; j--; }
                moves[j + 1] = m; sc[j + 1] = s;
            }
        }
    }
}
