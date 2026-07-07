/* Module registration for the NowUI WebGL FreeType build: outline fonts
 * only (TrueType + CFF/OpenType), autofit + PostScript hinting, smooth
 * rasterizer. No bitmap-only formats, no Type1/CID/PFR, no bzip2/brotli
 * streams — keeps the bundle small and free of symbols that could collide
 * with Unity's player libraries. */
FT_USE_MODULE( FT_Module_Class, autofit_module_class )
FT_USE_MODULE( FT_Driver_ClassRec, tt_driver_class )
FT_USE_MODULE( FT_Driver_ClassRec, cff_driver_class )
FT_USE_MODULE( FT_Module_Class, psaux_module_class )
FT_USE_MODULE( FT_Module_Class, psnames_module_class )
FT_USE_MODULE( FT_Module_Class, pshinter_module_class )
FT_USE_MODULE( FT_Module_Class, sfnt_module_class )
FT_USE_MODULE( FT_Renderer_Class, ft_smooth_renderer_class )
