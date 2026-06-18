using System;
using UnityEngine;

namespace NowUI
{
    public sealed class NowDialogView : INowView
    {
        interface IAction
        {
            void Invoke();
        }

        sealed class ActionCallback : IAction
        {
            readonly Action _callback;

            public ActionCallback(Action callback)
            {
                _callback = callback;
            }

            public void Invoke()
            {
                _callback?.Invoke();
            }
        }

        sealed class OwnerActionCallback<TOwner> : IAction
        {
            readonly TOwner _owner;
            readonly Action<TOwner> _callback;

            public OwnerActionCallback(TOwner owner, Action<TOwner> callback)
            {
                _owner = owner;
                _callback = callback;
            }

            public void Invoke()
            {
                _callback?.Invoke(_owner);
            }
        }

        const int DialogSeed = 0x4e444c47;
        const int AreaSeed = 0x4e444c41;
        const int PrimarySeed = 0x4e444c50;
        const int SecondarySeed = 0x4e444c53;

        static int s_nextId = 1;

        readonly int _id;
        readonly string _title;
        readonly string _message;
        readonly IAction _primaryAction;
        readonly IAction _secondaryAction;
        readonly bool _hasSecondary;

        string _primaryLabel;
        string _secondaryLabel;
        Vector2 _size;
        Color _scrimColor;
        bool _closeOnCancel;
        bool _closing;

        NowDialogView(
            string title,
            string message,
            string primaryLabel,
            IAction primaryAction,
            string secondaryLabel,
            IAction secondaryAction,
            bool hasSecondary)
        {
            _id = NowInput.CombineId(DialogSeed, NextId());
            _title = title ?? string.Empty;
            _message = message ?? string.Empty;
            _primaryLabel = string.IsNullOrEmpty(primaryLabel) ? "OK" : primaryLabel;
            _primaryAction = primaryAction;
            _secondaryLabel = string.IsNullOrEmpty(secondaryLabel) ? "Cancel" : secondaryLabel;
            _secondaryAction = secondaryAction;
            _hasSecondary = hasSecondary;
            _size = new Vector2(380f, 190f);
            _scrimColor = new Color(0f, 0f, 0f, 0.42f);
            _closeOnCancel = true;
        }

        public NowDialogView SetPrimaryLabel(string label)
        {
            if (!string.IsNullOrEmpty(label))
                _primaryLabel = label;

            return this;
        }

        public NowDialogView SetSecondaryLabel(string label)
        {
            if (!string.IsNullOrEmpty(label))
                _secondaryLabel = label;

            return this;
        }

        public NowDialogView SetSize(float width, float height)
        {
            _size = new Vector2(Mathf.Max(220f, width), Mathf.Max(120f, height));
            return this;
        }

        public NowDialogView SetScrim(Color color)
        {
            _scrimColor = color;
            return this;
        }

        public NowDialogView SetCloseOnCancel(bool value)
        {
            _closeOnCancel = value;
            return this;
        }

        public void Draw(NowViewContext context)
        {
            var theme = NowTheme.themeAsset;
            var surface = context.rect;

            if (_scrimColor.a > 0f)
            {
                var scrim = _scrimColor;
                scrim.a *= Mathf.Clamp01(context.visibleT);
                Now.Rectangle(surface).SetColor(scrim).Draw();
            }

            var panel = Center(surface, _size);
            theme.controlRenderer.DrawPopupBackground(theme, panel, menu: false);

            using (NowLayout.Area(NowInput.CombineId(_id, AreaSeed), panel, spacing: 12f, padding: 18f, alignItems: NowLayoutAlign.Start))
            {
                NowLayout.Label(NowControls.Text(theme, NowTextStyle.Title), _title)
                    .SetStretchWidth()
                    .Draw();

                NowLayout.Label(NowControls.Text(theme, NowTextStyle.Body), _message)
                    .SetStretchWidth()
                    .SetHeight(Mathf.Max(38f, panel.height - 132f))
                    .Draw();

                using (NowLayout.Horizontal(height: 34f, stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 8f))
                {
                    NowLayout.FlexibleSpace();

                    if (_hasSecondary &&
                        NowLayout.Button(_secondaryLabel)
                            .SetId(NowInput.CombineId(_id, SecondarySeed))
                            .SetStyle(NowRectangleStyle.Surface)
                            .SetWidth(104f)
                            .Draw())
                    {
                        Close(context, primary: false);
                    }

                    if (NowLayout.Button(_primaryLabel)
                        .SetId(NowInput.CombineId(_id, PrimarySeed))
                        .SetStyle(NowRectangleStyle.Accent)
                        .SetWidth(104f)
                        .Draw())
                    {
                        Close(context, primary: true);
                    }
                }
            }

            if (_closeOnCancel && NowInput.hasContext && NowInput.current.cancelPressed)
                Close(context, primary: !_hasSecondary);
        }

        void Close(NowViewContext context, bool primary)
        {
            if (_closing)
                return;

            _closing = true;

            if (primary)
                _primaryAction?.Invoke();
            else
                _secondaryAction?.Invoke();

            context.Close();
        }

        static NowRect Center(NowRect surface, Vector2 size)
        {
            float width = Mathf.Min(size.x, Mathf.Max(1f, surface.width));
            float height = Mathf.Min(size.y, Mathf.Max(1f, surface.height));
            return new NowRect(
                surface.x + (surface.width - width) * 0.5f,
                surface.y + (surface.height - height) * 0.5f,
                width,
                height);
        }

        static int NextId()
        {
            unchecked
            {
                int result = s_nextId++;

                if (s_nextId == 0)
                    s_nextId = 1;

                return result != 0 ? result : 1;
            }
        }

        internal static NowDialogView MessageBox(string title, string message, Action onClosed)
        {
            return new NowDialogView(
                title,
                message,
                "OK",
                new ActionCallback(onClosed),
                null,
                null,
                hasSecondary: false);
        }

        internal static NowDialogView MessageBox<TOwner>(string title, string message, TOwner owner, Action<TOwner> onClosed)
        {
            return new NowDialogView(
                title,
                message,
                "OK",
                new OwnerActionCallback<TOwner>(owner, onClosed),
                null,
                null,
                hasSecondary: false);
        }

        internal static NowDialogView Confirm(string title, string message, Action onConfirm, Action onCancel)
        {
            return new NowDialogView(
                title,
                message,
                "OK",
                new ActionCallback(onConfirm),
                "Cancel",
                new ActionCallback(onCancel),
                hasSecondary: true);
        }

        internal static NowDialogView Confirm<TOwner>(
            string title,
            string message,
            TOwner owner,
            Action<TOwner> onConfirm,
            Action<TOwner> onCancel)
        {
            return new NowDialogView(
                title,
                message,
                "OK",
                new OwnerActionCallback<TOwner>(owner, onConfirm),
                "Cancel",
                new OwnerActionCallback<TOwner>(owner, onCancel),
                hasSecondary: true);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            s_nextId = 1;
        }
    }

    public static partial class NowViews
    {
        public static NowDialogView MessageBox(string title, string message, Action onClosed = null)
        {
            return NowDialogView.MessageBox(title, message, onClosed);
        }

        public static NowDialogView MessageBox<TOwner>(
            string title,
            string message,
            TOwner owner,
            Action<TOwner> onClosed)
        {
            return NowDialogView.MessageBox(title, message, owner, onClosed);
        }

        public static NowDialogView Confirm(
            string title,
            string message,
            Action onConfirm,
            Action onCancel = null)
        {
            return NowDialogView.Confirm(title, message, onConfirm, onCancel);
        }

        public static NowDialogView Confirm<TOwner>(
            string title,
            string message,
            TOwner owner,
            Action<TOwner> onConfirm,
            Action<TOwner> onCancel = null)
        {
            return NowDialogView.Confirm(title, message, owner, onConfirm, onCancel);
        }
    }
}
