﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Hypernex.Configuration;
using Logger = Hypernex.CCK.Logger;

namespace Hypernex.Tools
{
    public static class DownloadTools
    {
        internal static string DownloadsPath;
        private static readonly Dictionary<string, byte[]> Cache = new();
        private static readonly Queue<DownloadMeta> Queue = new();
        private static readonly Dictionary<DownloadMeta, Thread> RunningThreads = new();

        public static void DownloadBytes(string url, Action<byte[]> OnDownload, Action<float> DownloadProgress = null, bool skipCache = false)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;
            try
            {
                if (Cache.ContainsKey(url) && !skipCache)
                {
                    QuickInvoke.InvokeActionOnMainThread(OnDownload, Cache[url]);
                    return;
                }
            }
            catch(Exception){ClearCache();}
            DownloadMeta meta = new DownloadMeta
            {
                url = url,
                done = OnDownload,
                skipCache = skipCache
            };
            if (DownloadProgress != null)
                meta.progress = DownloadProgress;
            Queue.Enqueue(meta);
            Logger.CurrentLogger.Debug("Added " + url + " to download queue!");
            Check();
        }

        private static string GetFileHash(string file)
        {
            using MD5 md5 = MD5.Create();
            using FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite,
                FileShare.ReadWrite | FileShare.Delete);
            byte[] hash = md5.ComputeHash(fileStream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public static void DownloadFile(string url, string output, Action<string> OnDownload,
            string knownFileHash = null, Action<DownloadProgressChangedEventArgs> DownloadProgress = null)
        {
            if (!Directory.Exists(DownloadsPath))
                Directory.CreateDirectory(DownloadsPath);
            string fileOutput = Path.Combine(DownloadsPath, output);
            bool fileExists = File.Exists(fileOutput);
            bool isHashSame = false;
            if (fileExists && !string.IsNullOrEmpty(knownFileHash))
                isHashSame = GetFileHash(fileOutput) == knownFileHash;
            if (fileExists && isHashSame)
                QuickInvoke.InvokeActionOnMainThread(OnDownload, fileOutput);
            else
            {
                DownloadMeta meta = new DownloadMeta
                {
                    url = url,
                    done = b =>
                    {
                        File.WriteAllBytes(fileOutput, b);
                        Array.Clear(b, 0, b.Length);
                        QuickInvoke.InvokeActionOnMainThread(OnDownload, fileOutput);
                    },
                    progress = p =>
                    {
                        if (DownloadProgress != null)
                            QuickInvoke.InvokeActionOnMainThread(DownloadProgress, p);
                    },
                    skipCache = true
                };
                Queue.Enqueue(meta);
                Logger.CurrentLogger.Debug("Added " + url + " to download queue!");
                Check();
            }
        }

        public static void ClearCache() => Cache.Clear();

        private static void Check()
        {
            if (Queue.Count <= 0 || RunningThreads.Count >= ConfigManager.LoadedConfig.DownloadThreads)
                return;
            if(RunningThreads.Count >= ConfigManager.LoadedConfig.DownloadThreads)
                return;
            DownloadMeta downloadMeta = Queue.Dequeue();
            Logger.CurrentLogger.Debug("Beginning Download for " + downloadMeta.url);
            Thread t = new Thread(async () =>
            {
                using HttpClient wc = new HttpClient();
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, downloadMeta.url);
                using var sendTask = wc.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                using var result = sendTask.Result.EnsureSuccessStatusCode();
                using var httpStream = await result.Content.ReadAsStreamAsync();
                long? leng = result.Content.Headers.ContentLength;
                using MemoryStream ms = new MemoryStream();
                if (leng.HasValue)
                {
                    var prog = new Progress<long>(total =>
                    {
                        if (downloadMeta.progress != null)
                            QuickInvoke.InvokeActionOnMainThread(downloadMeta.progress, total);
                    });
                    await CopyAsync(httpStream, ms, 81920, prog);
                }
                else
                {
                    await httpStream.CopyToAsync(ms);
                }
                Logger.CurrentLogger.Debug("Finished download for " + downloadMeta.url);
                if (!downloadMeta.skipCache)
                    AttemptAddToCache(downloadMeta.url, ms.ToArray());
                QuickInvoke.InvokeActionOnMainThread(downloadMeta.done, ms.ToArray());
                RunningThreads.Remove(downloadMeta);
            });
            RunningThreads.Add(downloadMeta, t);
            t.Start();
        }

        private static async Task CopyAsync(Stream source, Stream dest, int bufferSize, IProgress<long> progress)
        {
            var buffer = new byte[bufferSize];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) != 0)
            {
                await dest.WriteAsync(buffer, 0, read).ConfigureAwait(false);
                total += read;
                progress.Report(total);
            }
        }

        private static void AttemptAddToCache(string url, byte[] data)
        {
            // Count size (MB)
            double dataSize = data.Length / (1024.0 * 1024.0);
            if(dataSize > ConfigManager.LoadedConfig.MaxMemoryStorageCache)
                return;
            double s = 0.0;
            foreach (byte[] d in Cache.Values)
                s += d?.Length / (1024.0 * 1024.0) ?? 0;
            if (s >= ConfigManager.LoadedConfig.MaxMemoryStorageCache || s + dataSize >= ConfigManager.LoadedConfig.MaxMemoryStorageCache)
            {
                while (s >= ConfigManager.LoadedConfig.MaxMemoryStorageCache || s + dataSize >= ConfigManager.LoadedConfig.MaxMemoryStorageCache)
                {
                    if(Cache.Count <= 0)
                        break;
                    Cache.Remove(Cache.Last().Key);
                    foreach (byte[] d in Cache.Values)
                        s += d.Length / (1024.0 * 1024.0);
                }
            }
            if(!Cache.ContainsKey(url))
                Cache.Add(url, data);
        }
    }

    public class DownloadMeta
    {
        public string url;
        public Action<float> progress;
        public Action<byte[]> done;
        public bool skipCache;
    }
}