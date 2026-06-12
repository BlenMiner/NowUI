using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Immediate-mode draw call for a Lottie vector animation, mirroring the fluent
    /// style of <see cref="NowUIRectangle"/>:
    /// <code>Now.Lottie(rect, asset).SetTime(t).Draw();</code>
    /// </summary>
    [NowBuilder]
    public struct NowUILottie
    {
        public NowRect mask;

        public NowRect rect;

        public Vector4 color;

        public NowLottieAsset asset;

        /// <summary>Playback position in seconds.</summary>
        public float time;

        public bool loop;

        public bool preserveAspect;

        /// <summary>
        /// Playback frame rate cap; 0 plays at the animation's native rate. Lower rates
        /// (e.g. 15-20 for chat emoji) re-tessellate far less often with little visual
        /// difference, and identical frames are shared through the cache.
        /// </summary>
        public float playbackFrameRate;

        public NowUILottie(NowRect rect, NowLottieAsset asset)
        {
            mask = rect;
            this.rect = rect;
            this.asset = asset;
            color = new Vector4(1, 1, 1, 1);
            time = 0f;
            loop = true;
            preserveAspect = true;
            playbackFrameRate = 0f;
        }

        public NowUILottie SetPosition(NowRect rect)
        {
            this.rect = rect;
            return this;
        }

        public NowUILottie SetMask(NowRect mask)
        {
            this.mask = mask;
            return this;
        }

        public NowUILottie SetColor(Color color)
        {
            this.color = color;
            return this;
        }

        public NowUILottie SetColor(Vector4 color)
        {
            this.color = color;
            return this;
        }

        public NowUILottie SetTime(float seconds)
        {
            time = seconds;
            return this;
        }

        /// <summary>Sets the playback position from a 0..1 fraction of the animation.</summary>
        public NowUILottie SetNormalizedTime(float normalizedTime)
        {
            time = asset != null ? normalizedTime * asset.duration : 0f;
            return this;
        }

        /// <summary>Sets the playback position from a composition frame number.</summary>
        public NowUILottie SetFrame(float frame)
        {
            if (asset != null && asset.frameRate > 0f)
                time = (frame - asset.inPoint) / asset.frameRate;

            return this;
        }

        public NowUILottie SetLoop(bool loop)
        {
            this.loop = loop;
            return this;
        }

        /// <summary>When false the animation stretches to fill the rect.</summary>
        public NowUILottie SetPreserveAspect(bool preserveAspect)
        {
            this.preserveAspect = preserveAspect;
            return this;
        }

        /// <summary>Caps how often the animation advances; 0 = the animation's native rate.</summary>
        public NowUILottie SetPlaybackFrameRate(float framesPerSecond)
        {
            playbackFrameRate = framesPerSecond;
            return this;
        }

        [NowConsumer]
        public NowUILottie Draw()
        {
            Now.DrawLottie(this);
            return this;
        }
    }
}
