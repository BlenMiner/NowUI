using System;
using System.Runtime.InteropServices;
using System.Text;

internal static class NowUIFontCompilerNative
{
    const int ErrorCapacity = 4096;
    const string LibraryName = "nowui-msdf";

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    static extern int nowui_compile_font(
        string fontPath,
        string imagePath,
        string jsonPath,
        int size,
        int pixelRange,
        StringBuilder errorBuffer,
        int errorBufferLength);

    public static bool TryCompileFont(
        string fontPath,
        string imagePath,
        string jsonPath,
        int size,
        int pixelRange,
        out string error)
    {
        var errorBuffer = new StringBuilder(ErrorCapacity);

        try
        {
            int result = nowui_compile_font(
                fontPath,
                imagePath,
                jsonPath,
                size,
                pixelRange,
                errorBuffer,
                errorBuffer.Capacity);

            error = errorBuffer.ToString();

            if (result == 0)
                return true;

            if (string.IsNullOrWhiteSpace(error))
                error = "The native font compiler failed without an error message.";

            return false;
        }
        catch (DllNotFoundException ex)
        {
            error = "NowUI native font compiler plugin was not found for this editor platform.\n" + ex.Message;
            return false;
        }
        catch (EntryPointNotFoundException ex)
        {
            error = "NowUI native font compiler plugin is missing the nowui_compile_font entry point.\n" + ex.Message;
            return false;
        }
        catch (BadImageFormatException ex)
        {
            error = "NowUI native font compiler plugin has the wrong architecture for this editor.\n" + ex.Message;
            return false;
        }
    }
}
