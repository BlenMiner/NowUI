using System;
using UnityEngine;
using NowUI;

/// <summary>
/// Deterministic pointer-driven popup fixture. Input is authored through a
/// <see cref="NowInputReplay"/> and every driven frame advances the overlay
/// frame, enters matching input/draw-list scopes, and flushes deferred popup
/// drawing before returning.
/// </summary>
public sealed class NowPopupTestDriver : IDisposable
{
    readonly Vector2 _surface;
    bool _disposed;

    public NowPopupTestDriver(Vector2 surface)
    {
        if (surface.x <= 0f || surface.y <= 0f)
            throw new ArgumentOutOfRangeException(nameof(surface), "Popup test surfaces must be non-empty.");

        _surface = surface;
        replay = new NowInputReplay();
        drawList = new NowDrawList();
    }

    public NowInputReplay replay { get; }

    public NowDrawList drawList { get; }

    public Vector2 surface => _surface;

    /// <summary>
    /// Draws the currently authored replay snapshot as a fresh popup frame.
    /// Prefer the pointer helpers below when authoring a new input step.
    /// </summary>
    public void Frame(Action drawFrame)
    {
        ThrowIfDisposed();

        if (drawFrame == null)
            throw new ArgumentNullException(nameof(drawFrame));

        NowOverlay.ForceNewFrame();

        using (NowInput.Begin(replay, _surface))
        using (drawList.Begin(_surface))
        {
            drawFrame();
            NowOverlay.Flush();
        }
    }

    public void Hover(Vector2 position, Action drawFrame)
    {
        replay.Move(position);
        Frame(drawFrame);
    }

    public void Press(
        Vector2 position,
        Action drawFrame,
        NowPointerButton button = NowPointerButton.Primary)
    {
        replay.Press(position, button);
        Frame(drawFrame);
    }

    public void Release(
        Vector2 position,
        Action drawFrame,
        NowPointerButton button = NowPointerButton.Primary)
    {
        replay.Release(position, button);
        Frame(drawFrame);
    }

    public void Idle(Action drawFrame, bool hasPointer = true)
    {
        replay.Idle(hasPointer);
        Frame(drawFrame);
    }

    public void Scroll(Vector2 position, Vector2 delta, Action drawFrame)
    {
        replay.Scroll(position, delta);
        Frame(drawFrame);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        drawList.Dispose();
    }

    void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(NowPopupTestDriver));
    }
}
