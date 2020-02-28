// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Overlays
{
    /// <summary>
    /// An <see cref="IImageProcessor{TPixel}"/> that applies a radial glow effect an <see cref="Image{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class GlowProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly PixelBlender<TPixel> blender;
        private readonly GlowProcessor definition;

        /// <summary>
        /// Initializes a new instance of the <see cref="GlowProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="GlowProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public GlowProcessor(Configuration configuration, GlowProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.definition = definition;
            this.blender = PixelOperations<TPixel>.Instance.GetPixelBlender(definition.GraphicsOptions);
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            TPixel glowColor = this.definition.GlowColor.ToPixel<TPixel>();
            float blendPercent = this.definition.GraphicsOptions.BlendPercentage;

            var interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds());

            Vector2 center = Rectangle.Center(interest);
            float finalRadius = this.definition.Radius.Calculate(interest.Size);
            float maxDistance = finalRadius > 0
                ? MathF.Min(finalRadius, interest.Width * .5F)
                : interest.Width * .5F;

            Configuration configuration = this.Configuration;
            MemoryAllocator allocator = configuration.MemoryAllocator;

            using IMemoryOwner<TPixel> rowColors = allocator.Allocate<TPixel>(interest.Width);
            rowColors.GetSpan().Fill(glowColor);

            var operation = new RowIntervalOperation(configuration, interest, rowColors, this.blender, center, maxDistance, blendPercent, source);
            ParallelRowIterator.IterateRows<RowIntervalOperation, float>(
                configuration,
                interest,
                in operation);
        }

        private readonly struct RowIntervalOperation : IRowIntervalOperation<float>
        {
            private readonly Configuration configuration;
            private readonly Rectangle bounds;
            private readonly PixelBlender<TPixel> blender;
            private readonly Vector2 center;
            private readonly float maxDistance;
            private readonly float blendPercent;
            private readonly IMemoryOwner<TPixel> colors;
            private readonly ImageFrame<TPixel> source;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowIntervalOperation(
                Configuration configuration,
                Rectangle bounds,
                IMemoryOwner<TPixel> colors,
                PixelBlender<TPixel> blender,
                Vector2 center,
                float maxDistance,
                float blendPercent,
                ImageFrame<TPixel> source)
            {
                this.configuration = configuration;
                this.bounds = bounds;
                this.colors = colors;
                this.blender = blender;
                this.center = center;
                this.maxDistance = maxDistance;
                this.blendPercent = blendPercent;
                this.source = source;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows, Span<float> span)
            {
                Span<TPixel> colorSpan = this.colors.GetSpan();

                for (int y = rows.Min; y < rows.Max; y++)
                {
                    for (int i = 0; i < this.bounds.Width; i++)
                    {
                        float distance = Vector2.Distance(this.center, new Vector2(i + this.bounds.X, y));
                        span[i] = (this.blendPercent * (1 - (.95F * (distance / this.maxDistance)))).Clamp(0, 1);
                    }

                    Span<TPixel> destination = this.source.GetPixelRowSpan(y).Slice(this.bounds.X, this.bounds.Width);

                    this.blender.Blend(
                        this.configuration,
                        destination,
                        destination,
                        colorSpan,
                        span);
                }
            }
        }
    }
}
