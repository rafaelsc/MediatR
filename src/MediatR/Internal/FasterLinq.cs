namespace MediatR.Internal;

using System.Collections.Generic;
using System.Linq;

internal static class FasterLinq
{
    public static IEnumerable<T> BetterReverse<T>(this IEnumerable<T> source) => source switch
    {
        T[] arr => ReverseArray(arr),
        IReadOnlyList<T> lst => ReverseList(lst),
        IList<T> lst => ReverseList(lst),
        _ => source.Reverse()
    };

    private static IEnumerable<T> ReverseArray<T>(T[] arr)
    {
        for (int i = arr.Length - 1; i > 0; i--)
        {
            yield return arr[i];
        }
    }

    private static IEnumerable<T> ReverseList<T>(IReadOnlyList<T> lst)
    {
        for (int i = lst.Count - 1; i > 0; i--)
        {
            yield return lst[i];
        }
    }

    private static IEnumerable<T> ReverseList<T>(IList<T> lst)
    {
        for (int i = lst.Count - 1; i > 0; i--)
        {
            yield return lst[i];
        }
    }
}