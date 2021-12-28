using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PicOrganizer.Services
{
    public static class DirectoryInfoExtensions
    {
        public static List<FileInfo> GetFilesViaPattern(this DirectoryInfo source, string searchPatterns, SearchOption searchOption)
        {
            if (string.IsNullOrEmpty(searchPatterns))
                return new List<FileInfo>();
            if (searchPatterns.Contains("|"))
            {
                string[] searchPattern = searchPatterns.Split('|');
                List<FileInfo> result = new();
                for (int i = 0; i < searchPattern.Length; i++)
                {
                    result.AddRange(source.GetFilesViaPattern(searchPattern[i], searchOption));
                }
                return result;
            }
            else
                return source.GetFiles(searchPatterns, searchOption).ToList();
        }
    }

    public static class IEnumerableExtensions
    {
        //https://github.com/houseofcat/Tesseract/blob/master/src/HouseofCat.Extensions/IEnumerableExtensions.cs

        public static Task ParallelForEachAsync<T>(this IEnumerable<T> source, Func<T, Task> funcBody, int maxDoP )
        {
            async Task AwaitPartition(IEnumerator<T> partition)
            {
                using (partition)
                {
                    while (partition.MoveNext())
                    {
                        await Task.Yield();
                        await funcBody(partition.Current)
                            .ConfigureAwait(false);
                    }
                }
            }

            return Task.WhenAll(
                Partitioner
                    .Create(source)
                    .GetPartitions(maxDoP)
                    .AsParallel()
                    .Select(AwaitPartition));
        }

        public static Task ParallelForEachAsync<T1, T2>(this IEnumerable<T1> source, Func<T1, T2, Task> funcBody, T2 secondInput, int maxDoP )
        {
            async Task AwaitPartition(IEnumerator<T1> partition)
            {
                using (partition)
                {
                    while (partition.MoveNext())
                    {
                        await Task.Yield();
                        await funcBody(partition.Current, secondInput).ConfigureAwait(false);
                    }
                }
            }

            return Task.WhenAll(
                Partitioner
                    .Create(source)
                    .GetPartitions(maxDoP)
                    .AsParallel()
                    .Select(AwaitPartition));
        }

        public static Task ParallelForEachAsync<T1, T2, T3>(this IEnumerable<T1> source, Func<T1, T2, T3, Task> funcBody, T2 secondInput, T3 thirdInput, int maxDoP )
        {
            async Task AwaitPartition(IEnumerator<T1> partition)
            {
                using (partition)
                {
                    while (partition.MoveNext())
                    {
                        await Task.Yield();
                        await funcBody(partition.Current, secondInput, thirdInput).ConfigureAwait(false);
                    }
                }
            }

            return Task.WhenAll(
                Partitioner
                    .Create(source)
                    .GetPartitions(maxDoP)
                    .AsParallel()
                    .Select(AwaitPartition));
        }

        public static Task ParallelForEachAsync<T1, T2, T3, T4>(this IEnumerable<T1> source, Func<T1, T2, T3, T4, Task> funcBody, T2 secondInput, T3 thirdInput, T4 fourthInput, int maxDoP)
        {
            async Task AwaitPartition(IEnumerator<T1> partition)
            {
                using (partition)
                {
                    while (partition.MoveNext())
                    {
                        await Task.Yield();
                        await funcBody(partition.Current, secondInput, thirdInput, fourthInput).ConfigureAwait(false);
                    }
                }
            }

            return Task.WhenAll(
                Partitioner
                    .Create(source)
                    .GetPartitions(maxDoP)
                    .AsParallel()
                    .Select(AwaitPartition));
        }
    }
}
