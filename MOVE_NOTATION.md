# Manu et Mente — Move Notation

A compact, chess‑style way to record a game on paper. Just as chess writes
`R#f7` (a piece symbol, an action marker, a destination square), every move here
is written as:

```
[Piece] [origin][action][destination][marker]
```

You can drop parts that aren't needed (see *Short forms*), but when in doubt
write the long form — the board has many identical pieces, so being explicit
prevents ambiguity.

---

## 1. The board: naming a square

The board is seven concentric rings. Each ring has a **letter**; each cell in a
ring has a **place number**. A square is the letter followed by the number, e.g.
`A27`, `c5`, `D2`.

| Ring letter | Ring # | Cells | Position        |
|:-----------:|:------:|:-----:|-----------------|
| `A`         | 1      | 48    | Outermost       |
| `a`         | 2      | 48    |                 |
| `B`         | 3      | 24    |                 |
| `b`         | 4      | 24    |                 |
| `C`         | 5      | 12    |                 |
| `c`         | 6      | 12    |                 |
| `D`         | 7      | 4     | Center (goal)   |

**Orientation (so both players agree).** On each ring, **place 1** sits just
counter‑clockwise of the 3‑o'clock (due‑east) mark, and place numbers **increase
counter‑clockwise**. Always read place numbers off the same printed board so the
two players use identical coordinates.

> Because rings have different sizes, the same place number does **not** point the
> same direction across rings. A square is only meaningful as *letter + number*
> together (`B12` is not radially in line with `A12`).

---

## 2. Piece symbols

| Symbol | Piece       | Notes                                   |
|:------:|-------------|-----------------------------------------|
| `H`    | Hand        | Commander                               |
| `M`    | Mind        | Commander (internal code `B`)           |
| `T`    | Technician  |                                         |
| `W`    | Sage        |                                         |
| `S`    | Scholar     |                                         |
| `P`    | Skill       | Front rank — the "pawn"                 |
| `A`    | Apprentice  |                                         |

**Reading rule (resolving `A` the piece vs `A` the ring):** a *square* is always
`ring‑letter + digits`. The leading capital that comes **before** such a group is
the *piece*. So `A A27` (or `AA27`) = the **A**pprentice moving to square **A27**.
When recording by hand, a space between piece and square removes all doubt:
`A A27`, `M c2`, `H D1`.

---

## 3. Action markers (between origin and destination)

| Marker  | Meaning                                                        |
|:-------:|----------------------------------------------------------------|
| `-`     | Quiet move (no capture)                                         |
| `x`     | Capture                                                         |
| `e.p.`  | En‑passant capture (written *after* the move: `… x… e.p.`)     |

## 4. Suffix markers (end of the move)

| Marker  | Meaning                                                                 |
|:-------:|-------------------------------------------------------------------------|
| `+`     | **Threat** — this move threatens to win next turn (the "check")          |
| `#`     | **Winning move** — the move ends the game (the "mate")                   |
| `!` `?` | Optional commentary: `!` good, `?` dubious, `!!` brilliant, `??` blunder |

A move earns `#` when it triggers a win condition:

1. The mover now holds **2.0 points in the D ring** — Hand `1.0`, Mind `1.0`,
   Skill `0.33` each.
2. The mover **captures the opponent's last commander** (i.e. the opponent now
   has neither a Hand nor a Mind on the board).

---

## 5. Ring rotation moves

A Sage or Scholar may rotate a ring **pair** by 90°. Record it as the piece, its
square, then the pair and direction:

```
[Piece]([square]) [Pair][direction]
```

| Element     | Values                                  |
|-------------|-----------------------------------------|
| Pair        | `Aa`, `Bb`, `Cc`, `D`                   |
| Direction   | `↻` = clockwise (CW), `↺` = counter‑CW  |

Plain‑text directions `CW` / `ACW` are fine when arrows are awkward to write.

**Examples**
- `W(c4) Cc↻` — the Sage on c4 rotates ring pair **Cc** clockwise.
- `S(B7) Bb↺` — the Scholar on B7 rotates ring pair **Bb** counter‑clockwise.
- `S(D2) D↻+` — Scholar rotates the center ring clockwise, creating a winning threat.

---

## 6. Short forms

Like chess, you may shorten a move **when it is unambiguous**:

- **Drop the origin** when only one piece of that type can legally reach the
  destination: `HxD2` instead of `H A31xD2`.
- **Keep a partial origin** to disambiguate two candidates — give just the ring
  or just the place: `Pa- b6` vs `PA- b6`, or `P34- a33`.
- **Default to the long form** (full origin + destination) for hand‑written
  scoresheets. With 8 Skills and 2 Apprentices per side, explicit origins save
  arguments later.

---

## 7. Move numbering and a sample scoresheet

Orange moves first. Number each full turn `n. <Orange> <Green>`, exactly like
chess.

```
  ORANGE          GREEN
1. P a31-b18      P a6-b6
2. T A32-a32      W(A5) Aa↺
3. H A31-a31      M A6-a6
4. P b18xc9       Px… e.p. ... 
   …
17. M c2-D1+      H A7-a7
18. H a31-D3#     1–0   (Orange: 2 points in D)
```

*(The line above only illustrates the **format** — it is not a verified legal
game.)*

**Result tags:** `1–0` Orange wins, `0–1` Green wins, `½–½` draw (e.g. an agreed
or capped game with neither win condition met).

---

## 8. Worked examples

| Written            | Meaning                                                                 |
|--------------------|-------------------------------------------------------------------------|
| `H A31-c5`         | Hand moves from A31 to c5.                                               |
| `P a30xA29`        | Skill on a30 captures the piece on A29.                                  |
| `P a30xb6 e.p.`    | Skill on a30 captures en passant, landing on b6.                        |
| `A A34-a33`        | Apprentice moves from A34 to a33.                                       |
| `T B3xC1+`         | Technician captures on C1 and now threatens to win next turn.           |
| `W(c4) Cc↻`        | Sage on c4 rotates ring pair Cc clockwise.                              |
| `M c2-D1#`         | Mind enters D1, reaching 2 points in the center — game over.             |
| `HxD3#`            | Hand captures on D3, taking the opponent's last commander — game over.   |

---

## Appendix — Starting position

**Orange** (moves first)

| Square | Piece          | Square | Piece          |
|--------|----------------|--------|----------------|
| A27    | `A` Apprentice | a27    | `P` Skill      |
| A28    | `S` Scholar    | a28    | `P` Skill      |
| A29    | `W` Sage       | a29    | `P` Skill      |
| A30    | `M` Mind       | a30    | `P` Skill      |
| A31    | `H` Hand       | a31    | `P` Skill      |
| A32    | `T` Technician | a32    | `P` Skill      |
| A33    | `S` Scholar    | a33    | `P` Skill      |
| A34    | `A` Apprentice | a34    | `P` Skill      |

**Green**

| Square | Piece          | Square | Piece          |
|--------|----------------|--------|----------------|
| A3     | `A` Apprentice | a3     | `P` Skill      |
| A4     | `S` Scholar    | a4     | `P` Skill      |
| A5     | `W` Sage       | a5     | `P` Skill      |
| A6     | `M` Mind       | a6     | `P` Skill      |
| A7     | `H` Hand       | a7     | `P` Skill      |
| A8     | `T` Technician | a8     | `P` Skill      |
| A9     | `S` Scholar    | a9     | `P` Skill      |
| A10    | `A` Apprentice | a10    | `P` Skill      |
