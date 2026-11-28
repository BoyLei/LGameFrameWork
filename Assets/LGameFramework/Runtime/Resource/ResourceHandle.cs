using System;
using System.Threading;

namespace LGameFramework.Resource
{
    /// <summary>单次加载的所有权凭证。重复释放安全；资源不再使用后必须在创建线程释放。</summary>
    public sealed class ResourceHandle<T> : IDisposable
    {
        private readonly T value;
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;
        private Action release;
        public bool IsReleased => release == null;
        public T Asset => !IsReleased ? value : throw new ObjectDisposedException(nameof(ResourceHandle<T>));

        internal ResourceHandle(T value, Action release)
        {
            this.value = value;
            this.release = release ?? throw new ArgumentNullException(nameof(release));
        }

        public void Dispose()
        {
            if (Thread.CurrentThread.ManagedThreadId != threadId)
                throw new InvalidOperationException("Resource handles must be released on their creating Unity thread.");
            var callback = release;
            release = null;
            callback?.Invoke();
        }
    }
}
