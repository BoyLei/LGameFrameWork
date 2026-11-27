using System;
using System.Collections.Generic;
using System.Threading;

namespace LGameFramework.Resource
{
    /// <summary>页面/关卡的资源所有权集合；成功加载后调用 Own，退出时 Dispose。</summary>
    public sealed class ResourceScope : IDisposable
    {
        private readonly List<IDisposable> handles = new List<IDisposable>();
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;
        private bool disposed;

        public T Own<T>(T handle) where T : class, IDisposable
        {
            CheckThread();
            if (handle == null) throw new ArgumentNullException(nameof(handle));
            // 作用域可能在 await 加载期间关闭；迟到的句柄也必须回收。
            if (disposed)
            {
                handle.Dispose();
                throw new ObjectDisposedException(nameof(ResourceScope));
            }
            handles.Add(handle);
            return handle;
        }

        public void Dispose()
        {
            CheckThread();
            if (disposed) return;
            disposed = true;
            var errors = new List<Exception>();
            for (var i = handles.Count - 1; i >= 0; i--)
            {
                try { handles[i].Dispose(); }
                catch (Exception exception) { errors.Add(exception); }
            }
            handles.Clear();
            if (errors.Count > 0) throw new AggregateException(errors);
        }

        private void CheckThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != threadId)
                throw new InvalidOperationException("ResourceScope must be used on its creating Unity thread.");
        }
    }
}
