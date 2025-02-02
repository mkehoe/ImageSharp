﻿// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Memory.Internals
{
    /// <summary>
    /// Defines a strategy for managing unmanaged memory ownership.
    /// </summary>
    internal abstract class UnmanagedBufferLifetimeGuard : RefCountedLifetimeGuard
    {
        private UnmanagedMemoryHandle handle;

        protected UnmanagedBufferLifetimeGuard(UnmanagedMemoryHandle handle) => this.handle = handle;

        public ref UnmanagedMemoryHandle Handle => ref this.handle;

        public sealed class FreeHandle : UnmanagedBufferLifetimeGuard
        {
            public FreeHandle(UnmanagedMemoryHandle handle)
                : base(handle)
            {
            }

            protected override void Release() => this.Handle.Free();
        }
    }
}
