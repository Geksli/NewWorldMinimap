﻿using System;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using NewWorldMinimap.Core.Util;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TesserNet;

namespace NewWorldMinimap.Core
{
    /// <summary>
    /// Provides logic for performing OCR to find the position of the player.
    /// </summary>
    /// <seealso cref="IDisposable" />
    public class PositionDetector : IDisposable
    {
        private const int XOffset = 277;
        private const int YOffset = 18;
        private const int TextWidth = 277;
        private const int TextHeight = 18;
        private const int MaxCounter = 5;

        private static readonly Regex PosRegex = new Regex(@"(\d+ \d+) (\d+ \d+)", RegexOptions.Compiled);

        private readonly ITesseract tesseract = new TesseractPool(new TesseractOptions
        {
            PageSegmentation = PageSegmentation.Line,
            Numeric = true,
            Whitelist = "[]0123456789 ,.",
        });

        private bool disposedValue;
        private float lastX;
        private float lastY;
        private int counter = int.MaxValue;

        /// <summary>
        /// Finalizes an instance of the <see cref="PositionDetector"/> class.
        /// </summary>
        ~PositionDetector()
            => Dispose(false);

        /// <summary>
        /// Tries to get the position from the provided image.
        /// </summary>
        /// <param name="bmp">The image.</param>
        /// <param name="position">The position.</param>
        /// <returns>The found position.</returns>
        public bool TryGetPosition(Image<Rgba32> bmp, out Vector2 position)
        {
            /*
            bmp.Mutate(x => x
                .Crop(new Rectangle(bmp.Width - XOffset, YOffset, TextWidth, TextHeight))
                .Resize(TextWidth * 4, TextHeight * 4)
                .HistogramEqualization()
                .Crop(new Rectangle(0, 2 * 4, TextWidth * 4, 16 * 4))
                .WhiteFilter(0.9f)
                .Dilate(2)
                .Pad(TextWidth * 8, TextHeight * 16, Color.White));
            */
            string name = Guid.NewGuid().ToString();
            bmp.SaveAsPng($"a-{name}-0.png");

            bmp.Mutate(x => x
                .Crop(new Rectangle(bmp.Width - XOffset, YOffset + 2, TextWidth, 16))
                .Resize(new ResizeOptions()
                {
                    Sampler = KnownResamplers.NearestNeighbor,
                    Size = new Size(TextWidth * 3, 16 * 3),
                })
                .HslFilter());

            bmp.SaveAsPng($"a-{name}-1.png");

            if (TryGetPositionInternal(bmp, out position))
            {
                return true;
            }

            position = default;
            return false;
        }

        /// <summary>
        /// Resets the counter.
        /// </summary>
        public void ResetCounter()
            => counter = int.MaxValue;

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    tesseract.Dispose();
                }

                disposedValue = true;
            }
        }

        private bool TryGetPositionInternal(Image<Rgba32> bmp, out Vector2 position)
        {
            bmp.Metadata.HorizontalResolution = 300;
            bmp.Metadata.VerticalResolution = 300;

            string text = tesseract.Read(bmp).Trim();
            Console.WriteLine();
            Console.WriteLine("Read: " + text);
            text = Regex.Replace(text, @"[^0-9]+", " ");
            text = Regex.Replace(text, @"\s+", " ").Trim();
            Match m = PosRegex.Match(text);

            if (m.Success)
            {
                float x = float.Parse(m.Groups[1].Value.Replace(' ', '.'), CultureInfo.InvariantCulture);
                float y = float.Parse(m.Groups[2].Value.Replace(' ', '.'), CultureInfo.InvariantCulture);

                x %= 100000;

                while (x > 14260)
                {
                    x -= 10000;
                }

                y %= 10000;

                if (counter >= MaxCounter)
                {
                    counter = 0;
                }
                else
                {
                    if (Math.Abs(lastX - x) > 20 && counter < MaxCounter)
                    {
                        x = lastX;
                        counter++;
                    }

                    if (Math.Abs(lastY - y) > 20 && counter < MaxCounter)
                    {
                        y = lastY;
                        counter++;
                    }
                }

                if (x >= 4468 && x <= 14260 && y >= 84 && y <= 9999)
                {
                    lastX = x;
                    lastY = y;
                    position = new Vector2(x, y);
                    return true;
                }
            }

            position = default;
            return false;
        }
    }
}
