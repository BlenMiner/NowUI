using System.Runtime.CompilerServices;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Fluent row/column declaration. Configure the container, then call
    /// <see cref="Begin"/> in a using statement:
    /// <code>
    /// using (NowLayout.Column(view).Padding(16).Gap(12).Begin())
    /// using (NowLayout.Row().FillWidth().AlignChildren(NowLayoutAlign.Center).Begin())
    /// {
    ///     NowLayout.Label("Hello").Draw();
    /// }
    /// </code>
    /// </summary>
    [NowBuilder]
    public struct NowLayoutContainer
    {
        readonly bool _horizontal;

        readonly bool _hasRect;

        readonly NowRect _rect;

        readonly int _site;

        NowId _id;

        NowLayoutOptions _options;

        internal NowLayoutContainer(bool horizontal, int site)
        {
            _horizontal = horizontal;
            _hasRect = false;
            _rect = default;
            _site = site;
            _id = default;
            _options = default;
        }

        internal NowLayoutContainer(bool horizontal, NowRect rect, int site)
        {
            _horizontal = horizontal;
            _hasRect = true;
            _rect = rect;
            _site = site;
            _id = default;
            _options = default;
        }

        public NowLayoutContainer SetId(NowId id)
        {
            _id = id;
            return this;
        }

        public NowLayoutContainer Options(NowLayoutOptions options)
        {
            ValidateRootOptions(options);
            _options = options;
            return this;
        }

        public NowLayoutContainer Gap(float pixels)
        {
            _options = _options.SetSpacing(pixels);
            return this;
        }

        public NowLayoutContainer Padding(float all)
        {
            _options = _options.SetPadding(all);
            return this;
        }

        public NowLayoutContainer Padding(float horizontal, float vertical)
        {
            _options = _options.SetPadding(new Vector4(horizontal, vertical, horizontal, vertical));
            return this;
        }

        public NowLayoutContainer Padding(float left, float top, float right, float bottom)
        {
            _options = _options.SetPadding(new Vector4(left, top, right, bottom));
            return this;
        }

        public NowLayoutContainer Padding(Vector4 padding)
        {
            _options = _options.SetPadding(padding);
            return this;
        }

        public NowLayoutContainer Width(float width)
        {
            RequireNestedPlacement(nameof(Width));
            _options = _options.SetWidth(width);
            return this;
        }

        public NowLayoutContainer Height(float height)
        {
            RequireNestedPlacement(nameof(Height));
            _options = _options.SetHeight(height);
            return this;
        }

        public NowLayoutContainer MinWidth(float width)
        {
            RequireNestedPlacement(nameof(MinWidth));
            _options = _options.SetMinWidth(width);
            return this;
        }

        public NowLayoutContainer MaxWidth(float width)
        {
            RequireNestedPlacement(nameof(MaxWidth));
            _options = _options.SetMaxWidth(width);
            return this;
        }

        public NowLayoutContainer MinHeight(float height)
        {
            RequireNestedPlacement(nameof(MinHeight));
            _options = _options.SetMinHeight(height);
            return this;
        }

        public NowLayoutContainer MaxHeight(float height)
        {
            RequireNestedPlacement(nameof(MaxHeight));
            _options = _options.SetMaxHeight(height);
            return this;
        }

        public NowLayoutContainer FillWidth()
        {
            RequireNestedPlacement(nameof(FillWidth));
            _options = _options.SetStretchWidth();
            return this;
        }

        public NowLayoutContainer FillWidth(float maxWidth)
        {
            RequireNestedPlacement(nameof(FillWidth));
            _options = _options.SetStretchWidth().SetMaxWidth(maxWidth);
            return this;
        }

        public NowLayoutContainer FillHeight()
        {
            RequireNestedPlacement(nameof(FillHeight));
            _options = _options.SetStretchHeight();
            return this;
        }

        public NowLayoutContainer FillHeight(float maxHeight)
        {
            RequireNestedPlacement(nameof(FillHeight));
            _options = _options.SetStretchHeight().SetMaxHeight(maxHeight);
            return this;
        }

        public NowLayoutContainer Grow(float weight = 1f)
        {
            RequireNestedPlacement(nameof(Grow));
            _options = _options.SetGrow(weight);
            return this;
        }

        public NowLayoutContainer AlignSelf(NowLayoutAlign align)
        {
            RequireNestedPlacement(nameof(AlignSelf));
            _options = _options.SetAlign(align);
            return this;
        }

        public NowLayoutContainer AlignChildren(NowLayoutAlign align)
        {
            _options = _options.SetAlignItems(align);
            return this;
        }

        public NowLayoutContainer Justify(NowLayoutJustify justify)
        {
            _options = _options.SetJustify(justify);
            return this;
        }

        [NowConsumer]
        public NowLayoutScope Begin()
        {
            ValidateRootOptions(_options);
            return _hasRect
                ? NowLayout.BeginContainerArea(_horizontal, _rect, _id, _options, _site)
                : NowLayout.BeginGroup(_horizontal, _id, _options, _site);
        }

        readonly void RequireNestedPlacement(string method)
        {
            if (!_hasRect)
                return;

            throw new System.InvalidOperationException(
                $"{method} configures a container inside its parent and cannot be used on " +
                "NowLayout.Row(rect) or Column(rect), whose explicit rect already fixes the root bounds. " +
                "Change that rect, or use Row()/Column() inside a parent.");
        }

        readonly void ValidateRootOptions(in NowLayoutOptions options)
        {
            if (!_hasRect)
                return;

            const NowLayoutOptions.Field placementFields =
                NowLayoutOptions.Field.Width |
                NowLayoutOptions.Field.Height |
                NowLayoutOptions.Field.MinWidth |
                NowLayoutOptions.Field.MaxWidth |
                NowLayoutOptions.Field.MinHeight |
                NowLayoutOptions.Field.MaxHeight |
                NowLayoutOptions.Field.StretchWidth |
                NowLayoutOptions.Field.StretchHeight |
                NowLayoutOptions.Field.Align |
                NowLayoutOptions.Field.Grow;

            if ((options.fields & placementFields) == 0)
                return;

            throw new System.InvalidOperationException(
                "Root Row(rect)/Column(rect) options may configure only Gap, Padding, AlignChildren, and Justify. " +
                "The explicit rect already fixes the root's size and placement; put sizing, stretching, Grow, or " +
                "AlignSelf on a nested Row()/Column() instead.");
        }
    }

    public static partial class NowLayout
    {
        /// <summary>Declares a vertical container inside the active layout.</summary>
        public static NowLayoutContainer Column(
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return new NowLayoutContainer(false, NowControls.SiteId(file, line));
        }

        /// <summary>Declares a root vertical container over an explicit rect.</summary>
        public static NowLayoutContainer Column(
            NowRect rect,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return new NowLayoutContainer(false, rect, NowControls.SiteId(file, line));
        }

        /// <summary>Declares a horizontal container inside the active layout.</summary>
        public static NowLayoutContainer Row(
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return new NowLayoutContainer(true, NowControls.SiteId(file, line));
        }

        /// <summary>Declares a root horizontal container over an explicit rect.</summary>
        public static NowLayoutContainer Row(
            NowRect rect,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return new NowLayoutContainer(true, rect, NowControls.SiteId(file, line));
        }

        /// <summary>Alias for <see cref="FlexibleSpace"/> with layout-DSL terminology.</summary>
        public static void Spacer(float weight = 1f)
        {
            FlexibleSpace(weight);
        }

        internal static NowLayoutScope BeginContainerArea(
            bool horizontal,
            NowRect rect,
            NowId id,
            in NowLayoutOptions options,
            int site)
        {
            int fallback = HashCombine(AreaSeed, NowControls.GetControlId(site));
            return BeginArea(id.ResolveStableId(fallback), rect, options, horizontal);
        }
    }
}
