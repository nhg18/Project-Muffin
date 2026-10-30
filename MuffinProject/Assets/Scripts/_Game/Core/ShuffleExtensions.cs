using System;
using System.Collections.Generic;

namespace Chapchu.Game.Core
{
    public static class ShuffleExtensions
    {
        /// <summary>
        /// 제자리에서 섞는다 (Fisher–Yates). 랜덤은 밖에서 받는다 — 같은 시드면 같은 판이 나오게.
        /// </summary>
        public static void Shuffle<T>(this IList<T> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
