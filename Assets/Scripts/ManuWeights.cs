// SPDX-License-Identifier: MPL-2.0
// Evaluation weights for ManuEngine, produced by ManuTrainer self-play training.
// Retrained 2026-11-06 for the rule 'last commander in D + 4 pawns in C/c only after losing a commander': beat the previous weights 130-25-45 under the new rules (depth 3).
// Layout: indices 0..48 are a piece-square table [(type-1)*7 + (ring-1)] for types
// P,A,T,S,W,H,B and rings A,a,B,b,C,c,D; 49.. are the extra features listed in ManuAI.Eval.
// To retrain: Unity menu Manu AI > Train AI (writes Resources/ManuAIWeights.txt, which overrides this).
namespace ManuAI
{
    public static class ManuWeights
    {
        public const string ResourceName = "ManuAIWeights";

        public static readonly double[] Trained = {
        12.091, 15.197, 17.699, 16.766, 19.274, 20.682, 21.622,
        9.135, 8.454, 11.899, 12.257, 13.992, 14.774, 15.185,
        31.362, 35.862, 38.932, 42.512, 46.055, 48.609, 52.584,
        15.352, 15.159, 17.753, 20.116, 20.362, 21.22, 22.131,
        32.585, 35.707, 38.642, 41.693, 46.976, 48.231, 51.333,
        49.37, 53.812, 58.56, 64.58, 74.478, 89.989, 184.837,
        51.156, 52.7, 58.455, 64.42, 74.649, 88.971, 187.076,
        49.649, 38.716, 5.856, 29.914, -20.053, 4.741, 14.442,
        9.915, -1.423
        };
    }
}
