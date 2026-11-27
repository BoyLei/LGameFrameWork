using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LGameFramework.Resource
{
    internal sealed class AddressablesResourceBackend : IResourceBackend
    {
        // Addressables initialization is global. Share the pending handle across service instances
        // so two owners never release the same initialization reference.
        private static Task initialization;
        public Task InitializeAsync()
        {
            if (initialization == null || initialization.IsFaulted || initialization.IsCanceled)
                initialization = InitializeCoreAsync();
            return initialization;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInitialization() => initialization = null;

        private static async Task InitializeCoreAsync()
        {
            var handle = Addressables.InitializeAsync(false);
            try { await Await(handle); }
            finally { if (handle.IsValid()) Addressables.Release(handle); }
        }

        public IResourceOperation<T> Load<T>(string address) where T : UnityEngine.Object
        {
            var handle = Addressables.LoadAssetAsync<T>(address);
            return new Operation<T>(Await(handle), () => Addressables.Release(handle));
        }

        public IResourceOperation<GameObject> Instantiate(string address, Transform parent)
        {
            var handle = Addressables.InstantiateAsync(address, parent, false, true);
            return new Operation<GameObject>(Await(handle), () =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                    Addressables.ReleaseInstance(handle);
                else
                    Addressables.Release(handle);
            });
        }

        public IResourceOperation<long> GetDownloadSize(string key)
        {
            var handle = Addressables.GetDownloadSizeAsync(key);
            return new Operation<long>(Await(handle), () => Addressables.Release(handle));
        }

        public IResourceOperation<object> DownloadDependencies(string key)
        {
            var handle = Addressables.DownloadDependenciesAsync(key, false);
            return new Operation<object>(Await(handle), () => Addressables.Release(handle));
        }

        private static Task<T> Await<T>(AsyncOperationHandle<T> handle)
        {
            var completion = new TaskCompletionSource<T>();
            void Complete(AsyncOperationHandle<T> operation)
            {
                if (operation.Status == AsyncOperationStatus.Succeeded)
                    completion.TrySetResult(operation.Result);
                else
                    completion.TrySetException(operation.OperationException ?? new InvalidOperationException("Addressables operation failed."));
            }
            if (handle.IsDone) Complete(handle);
            else handle.Completed += Complete;
            return completion.Task;
        }

        private static Task<object> Await(AsyncOperationHandle handle)
        {
            var completion = new TaskCompletionSource<object>();
            void Complete(AsyncOperationHandle operation)
            {
                if (operation.Status == AsyncOperationStatus.Succeeded)
                    completion.TrySetResult(operation.Result);
                else
                    completion.TrySetException(operation.OperationException ?? new InvalidOperationException("Addressables operation failed."));
            }
            if (handle.IsDone) Complete(handle);
            else handle.Completed += Complete;
            return completion.Task;
        }

        private sealed class Operation<T> : IResourceOperation<T>
        {
            private Action release;
            public Task<T> Completion { get; }
            public Operation(Task<T> completion, Action release)
            {
                Completion = completion;
                this.release = release;
            }
            public void Dispose()
            {
                var callback = release;
                release = null;
                callback?.Invoke();
            }
        }
    }
}
