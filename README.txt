Implementation of Openslide API (with Deepzoom) in C# via Interop. Based on https://github.com/openslide/openslide-python/blob/master/openslide/deepzoom.py

You need to have Openslide's dlls (with correct architecture - x64 or x86) in your PATH or where the DLL loader can find them.

Usage 
            OpenSlide slides = new OpenSlide();
   
            if (Request.RawUrl.EndsWith(".dzi"))
                return File(slides.GetDZI(@"myslide.svs"), "application/xml");
            else
                return File(slides.GetJpg(@"myslide.svs", "_files/14/0_0.jpeg"), "image/jpeg");