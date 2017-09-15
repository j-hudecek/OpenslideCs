Implementation of Openslide API (with Deepzoom) in C# via Interop. Based on https://github.com/openslide/openslide-python/blob/master/openslide/deepzoom.py

You need .NET Core 2.0 installed to build it and run it.

To run on Linux you need to download https://jenkins.mono-project.com/view/Components/job/Components-SkiaSharp-Linux/51/Azure/processDownloadRequest/ArtifactsFor-51/e4f9defab7287fca3f0341c53c8a99ca4274f439/output/native/linux/x64/libSkiaSharp.so and manually copy it to bin folder.

You need to have Openslide's dlls (with correct architecture - x64 or x86) in your PATH or where the DLL loader can find them or install the openslide package on Linux (on CentOS you need EPEL)

Usage 
            OpenSlide slides = new OpenSlide();
   
            if (Request.RawUrl.EndsWith(".dzi"))
                return File(slides.GetDZI(@"myslide.svs"), "application/xml");
            else
                return File(slides.GetJpg(@"myslide.svs", "_files/14/0_0.jpeg"), "image/jpeg");