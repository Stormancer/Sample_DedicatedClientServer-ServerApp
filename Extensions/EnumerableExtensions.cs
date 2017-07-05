using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<IEnumerable<T>> GetCombinations<T>(this IEnumerable<T> enumerable, int combinationLength)
        {
            if (combinationLength == 0)
            {
                yield return Enumerable.Empty<T>();
                yield break;
            }

            if (!enumerable.Any())
            {
                yield break;
            }

            var t = enumerable.First();

            foreach (var combination in GetCombinations(enumerable.Skip(1), combinationLength))
            {
                yield return combination;
            }

            foreach (var combination in GetCombinations(enumerable.Skip(1), combinationLength - 1))
            {
                yield return new[] { t }.Concat(combination);
            }
        }

        public static IEnumerable<IEnumerable<T>> GetCombinations<T>(this IEnumerable<T> enumerable, int targetWeight, Func<T, int> weightSelector)
        {
            if(targetWeight == 0)
            {
                yield return Enumerable.Empty<T>();
                yield break;
            }

            if(!enumerable.Any() || targetWeight < 0)
            {
                yield break;
            }
            if(enumerable.Sum(weightSelector) == targetWeight)
            {
                yield return enumerable.ToList();
            }
            var t = enumerable.First();

            foreach(var combination in enumerable.Skip(1).GetCombinations(targetWeight, weightSelector))
            {
                yield return combination;
            }

            foreach (var combination in GetCombinations(enumerable.Skip(1), targetWeight - weightSelector(t)))
            {
                yield return new[] { t }.Concat(combination);
            }
        }

        private static readonly Random _rand = new Random();
        public static T RandomElement<T>(this IEnumerable<T> enumerable)
        {
            var candidates = enumerable.ToArray();

            return candidates[_rand.Next(candidates.Length)];
        }
    }
}
