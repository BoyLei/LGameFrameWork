using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LGameFramework.Resource
{
    /// <summary>管理本服务创建的所有句柄；Dispose 回收已完成加载，进行中的加载完成后自动回收。</summary>
    public sealed class ResourceService : IResourceService
    {
        private readonly IResourceBackend backend;
        private readonly HashSet<IDisposable> handles = new HashSet<IDisposable>();
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;
        private Task initialization;
        private bool disposed;

        public ResourceService() : this(new AddressablesResourceBackend()) { }
        internal ResourceService(IResourceBackend backend)
        {
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public async Task InitializeAsync()
        {
            CheckAvailable();
            // 失败后允许显式重试；同时初始化的调用共享同一个底层操作。
            if (initialization == null || initialization.IsFaulted || initialization.IsCanceled)
                initialization = backend.InitializeAsync();
            await initialization;
            CheckAvailable();
        }

        public async Task<ResourceHandle<T>> LoadAsync<T>(string address) where T : UnityEngine.Object
        {
            CheckKey(address);
            await InitializeAsync();
            return await OwnAsync(backend.Load<T>(address));
        }

        public async Task<ResourceHandle<GameObject>> InstantiateAsync(string address, Transform parent = null)
        {
            CheckKey(address);
            await InitializeAsync();
            return await OwnAsync(backend.Instantiate(address, parent));
        }

        public async Task<long> GetDownloadSizeAsync(string addressOrLabel)
        {
            CheckKey(addressOrLabel);
            await InitializeAsync();
            using (var operation = backend.GetDownloadSize(addressOrLabel))
            {
                var size = await operation.Completion;
                CheckAvailable();
                return size;
            }
        }

        public async Task DownloadDependenciesAsync(string addressOrLabel)
        {
            CheckKey(addressOrLabel);
            await InitializeAsync();
            using (var operation = backend.DownloadDependencies(addressOrLabel))
            {
                await operation.Completion;
                CheckAvailable();
            }
        }

        private async Task<ResourceHandle<T>> OwnAsync<T>(IResourceOperation<T> operation)
        {
            try
            {
                var asset = await operation.Completion;
                CheckAvailable();
                ResourceHandle<T> handle = null;
                handle = new ResourceHandle<T>(asset, () =>
                {
                    handles.Remove(handle);
                    operation.Dispose();
                });
                handles.Add(handle);
                return handle;
            }
            catch
            {
                operation.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            CheckThread();
            if (disposed) return;
            disposed = true;
            var errors = new List<Exception>();
            foreach (var handle in new List<IDisposable>(handles))
            {
                try { handle.Dispose(); }
                catch (Exception exception) { errors.Add(exception); }
            }
            handles.Clear();
            if (errors.Count > 0) throw new AggregateException(errors);
        }

        private void CheckAvailable()
        {
            CheckThread();
            if (disposed) throw new ObjectDisposedException(nameof(ResourceService));
        }

        private void CheckThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != threadId)
                throw new InvalidOperationException("ResourceService must be used on its creating Unity thread.");
        }

        private static void CheckKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Address or label cannot be empty.", nameof(key));
        }
    }
}
