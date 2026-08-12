using System;
using UnityEngine;
using NowUI;

public sealed class NowInputReplay : INowInputProvider, INowTextInputSource
{
    public NowInputSnapshot snapshot;
    public NowTextInputFrame textFrame;
    public bool hasInput = true;
    public int frame = 1;
    public float time = 1f;

    public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
    {
        result = snapshot;
        return hasInput;
    }

    public bool TryGetFrame(out NowTextInputFrame result)
    {
        result = textFrame;
        return true;
    }

    public void Idle(bool hasPointer = false)
    {
        textFrame = default;
        snapshot = new NowInputSnapshot(
            hasPointer,
            snapshot.pointerPosition,
            snapshot.pointerPosition,
            Vector2.zero,
            NowPointerButtons.None,
            NowPointerButtons.None,
            NowPointerButtons.None,
            Vector2.zero,
            Vector2.zero,
            false,
            false,
            false,
            false,
            false,
            false,
            frame++,
            time);
        time += 0.016f;
    }

    public void Move(Vector2 position)
    {
        SetPointer(position, Vector2.zero, NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None);
    }

    public void Press(Vector2 position, NowPointerButton button = NowPointerButton.Primary)
    {
        var mask = ToButtonMask(button);
        SetPointer(position, Vector2.zero, mask, mask, NowPointerButtons.None);
    }

    public void Drag(Vector2 position, Vector2 previousPosition, NowPointerButton button = NowPointerButton.Primary)
    {
        var mask = ToButtonMask(button);
        SetPointer(position, position - previousPosition, mask, NowPointerButtons.None, NowPointerButtons.None);
    }

    public void Release(Vector2 position, NowPointerButton button = NowPointerButton.Primary)
    {
        var mask = ToButtonMask(button);
        SetPointer(position, Vector2.zero, NowPointerButtons.None, NowPointerButtons.None, mask);
    }

    public void Scroll(Vector2 position, Vector2 delta)
    {
        textFrame = default;
        snapshot = new NowInputSnapshot(
            true,
            position,
            position,
            Vector2.zero,
            NowPointerButtons.None,
            NowPointerButtons.None,
            NowPointerButtons.None,
            delta,
            Vector2.zero,
            false,
            false,
            false,
            false,
            false,
            false,
            frame++,
            time);
        time += 0.016f;
    }

    public void Text(string characters)
    {
        textFrame = new NowTextInputFrame { characters = characters };
        snapshot = NavigationSnapshot(default);
    }

    public void Keys(NowTextInputFrame keys)
    {
        textFrame = keys;
        snapshot = NavigationSnapshot(default);
    }

    public void Navigate(Vector2 navigation, bool previous = false, bool next = false)
    {
        textFrame = default;
        snapshot = NavigationSnapshot(navigation, previous, next);
    }

    public IDisposable BeginTextInput()
    {
        NowTextInput.source = this;
        return new TextInputScope();
    }

    void SetPointer(
        Vector2 position,
        Vector2 delta,
        NowPointerButtons down,
        NowPointerButtons pressed,
        NowPointerButtons released)
    {
        textFrame = default;
        snapshot = new NowInputSnapshot(
            true,
            position,
            position - delta,
            delta,
            down,
            pressed,
            released,
            Vector2.zero,
            Vector2.zero,
            false,
            false,
            false,
            false,
            false,
            false,
            frame++,
            time);
        time += 0.016f;
    }

    static NowPointerButtons ToButtonMask(NowPointerButton button)
    {
        return button switch
        {
            NowPointerButton.Primary => NowPointerButtons.Primary,
            NowPointerButton.Secondary => NowPointerButtons.Secondary,
            NowPointerButton.Middle => NowPointerButtons.Middle,
            NowPointerButton.Back => NowPointerButtons.Back,
            NowPointerButton.Forward => NowPointerButtons.Forward,
            _ => NowPointerButtons.None
        };
    }

    NowInputSnapshot NavigationSnapshot(Vector2 navigation, bool previous = false, bool next = false)
    {
        return new NowInputSnapshot(
            false,
            default,
            default,
            default,
            NowPointerButtons.None,
            NowPointerButtons.None,
            NowPointerButtons.None,
            Vector2.zero,
            navigation,
            previous,
            next,
            false,
            false,
            false,
            false,
            false,
            false,
            frame++,
            time);
    }

    sealed class TextInputScope : IDisposable
    {
        public void Dispose()
        {
            NowTextInput.Reset();
        }
    }
}
