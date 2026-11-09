using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using Forms = System.Windows.Forms;

namespace Lucent
{
    // What the overlay should show right now. Zoom of 1 with no shadow lift means nothing is drawn.
    internal sealed class ZoomRequest
    {
        public double Zoom=1;
        // Zoom around the pointer, so whatever is under it stays put and the real pointer stays accurate. Otherwise the middle of the screen.
        public bool Follow;
        // 0 to 1: how much the magnified picture is sharpened, since stretching pixels softens it.
        public double Sharpness;
        // Adaptive sharpening: the shader weighs it by the local contrast and by how far it is zoomed.
        public bool Adaptive;
        // Smart Dark Boost: a shadow lift done per pixel on the GPU, which keeps bright areas from washing out.
        public double Lift,Black;
        public float[] Tint=new float[]{1,1,1};
        public bool HideCursor=true;
        // Color pop: the hue to make stand out, in turns from 0 to 1 (red is 0, green a third, blue two thirds), or -1 for none, and how hard.
        public double PopHue=-1,PopStrength;
        internal bool Wanted {get{return Zoom>1.001||Lift>0||(PopHue>=0&&PopStrength>0);}}
    }

    internal static class ColorPop
    {
        // The hue of each Color pop choice, in turns around the color wheel (red 0, green a third, blue two thirds); -1 for none.
        public static double HueFor(string color)
        {
            switch(color)
            {
                case "Red":return 0;
                case "Orange":return 30/360d;
                case "Yellow":return 58/360d;
                case "Green":return 120/360d;
                case "Cyan":return 180/360d;
                case "Blue":return 225/360d;
                case "Magenta":return 305/360d;
                default:return -1;
            }
        }
    }
    // The values the shader uses for one drawn frame.
    internal sealed class ZoomLook
    {
        public float Zoom=1,FocusX=.5f,FocusY=.5f,Sharpen,Lift,Black,PopHue=-1,PopStrength;
        public bool Adaptive;
        public float[] Tint=new float[]{1,1,1};
    }

    // Desktop capture and zoom stay in GPU textures.
    // The display is being changed (a new resolution, a game taking the screen): not a failure, the capture is set up again in a moment.
    internal sealed class DisplayNotReady : InvalidOperationException
    {
        public DisplayNotReady(string message):base(message){}
    }
    internal sealed class DesktopGpuZoom : IDisposable
    {
        volatile bool stopping;
        volatile ZoomRequest request=new ZoomRequest();
        Thread worker;
        public event Action<string> Failed;
        // Test hooks. The real overlay is always on the primary display and always hidden from screen capture.
        internal Forms.Screen Target=Forms.Screen.PrimaryScreen;
        // The overlay follows the primary display: after a display change it is looked up again, instead of staying with the display as it was.
        readonly Forms.Screen firstTarget;
        public DesktopGpuZoom(){firstTarget=Target;}
        Forms.Screen Live(){return ReferenceEquals(Target,firstTarget)||ReferenceEquals(Target,followed)?Forms.Screen.PrimaryScreen:Target;}
        Forms.Screen followed;
        internal bool AllowCapture;
        internal IntPtr OverlayHandle;
        internal long FramesDrawn;
        // Test hook: read these screen pixels back from each finished frame, before it is shown.
        internal int[][] SamplePoints;
        internal volatile int[][] Sampled;
        public void Apply(double zoomFactor){Apply(new ZoomRequest {Zoom=zoomFactor});}
        public void Apply(ZoomRequest next)
        {
            request=next;
            if(worker!=null)return;
            worker=new Thread(Run) {IsBackground=true,Name="Lucent GPU zoom"};
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
        }
        public void Dispose()
        {
            stopping=true;
            if(worker!=null&&worker!=Thread.CurrentThread)worker.Join(2500);
        }
        [DllImport("user32.dll",SetLastError=true)]
        static extern bool SetWindowDisplayAffinity(IntPtr window,uint affinity);
        sealed class DisplayWindow : Forms.Form
        {
            public DisplayWindow()
            {
                FormBorderStyle=Forms.FormBorderStyle.None;
                StartPosition=Forms.FormStartPosition.Manual;
                AutoScaleMode=Forms.AutoScaleMode.None;
                ShowInTaskbar=false;TopMost=true;
                // Until the first pictures are drawn into it, the window would show its plain background, which was a white flash.
                BackColor=System.Drawing.Color.Black;
                SetStyle(Forms.ControlStyles.AllPaintingInWmPaint|Forms.ControlStyles.Opaque|Forms.ControlStyles.UserPaint,true);
            }
            protected override void OnPaintBackground(Forms.PaintEventArgs e){}
            protected override void OnPaint(Forms.PaintEventArgs e){}
            int revealIn;
            // The window is shown fully see-through; after a couple of frames have been drawn into it, it becomes visible.
            public void RevealAfter(int frames){revealIn=frames;}
            public bool Revealing {get{return revealIn>0;}}
            public void FrameDrawn()
            {
                if(revealIn<=0)return;
                if(--revealIn==0)SetLayeredWindowAttributes(Handle,0,255,2);
            }
            protected override bool ShowWithoutActivation {get{return true;}}
            // No activation (0x08000000), layered (0x80000), tool window (0x80) and transparent (0x20). Without the layered bit the transparent
            // bit does not let the mouse through, and the overlay swallowed every click and key aimed at whatever was under it.
            protected override Forms.CreateParams CreateParams
            {
                get {var cp=base.CreateParams;cp.ExStyle|=0x080800A0;return cp;}
            }
            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                // A layered window is only shown once it has been given its opacity.
                SetLayeredWindowAttributes(Handle,0,0,2);
            }
            // Whatever the pointer is over, it is never over the overlay.
            protected override void WndProc(ref Forms.Message m)
            {
                if(m.Msg==0x0084){m.Result=(IntPtr)(-1);return;}
                base.WndProc(ref m);
            }
            [DllImport("user32.dll",SetLastError=true)]
            static extern bool SetLayeredWindowAttributes(IntPtr window,uint key,byte alpha,uint flags);
            [DllImport("user32.dll")]
            static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
            // Shown without activation, a window can end up beneath another topmost window. This puts the overlay on top of them all,
            // without moving, resizing or activating anything.
            public void ShowOnTop()
            {
                SetLayeredWindowAttributes(Handle,0,0,2);RevealAfter(3);
                Show();
                SetWindowPos(Handle,new IntPtr(-1),0,0,0,0,0x0001|0x0002|0x0010|0x0040);
            }
        }
        [DllImport("Magnification.dll")] static extern bool MagInitialize();
        [DllImport("Magnification.dll")] static extern bool MagShowSystemCursor(bool show);
        [DllImport("Magnification.dll")] static extern bool MagUninitialize();

        // A display change, a game taking the screen or a lock screen makes Windows end the capture. Those are not failures:
        // the capture is simply set up again, and only a problem that keeps coming back stops the zoom.
        static bool Recoverable(Exception error)
        {
            if(error is DisplayNotReady)return true;
            var problem=error as SharpDXException;
            if(problem==null)return false;
            var code=problem.ResultCode;
            return code==SharpDX.DXGI.ResultCode.AccessLost||code==SharpDX.DXGI.ResultCode.DeviceRemoved||code==SharpDX.DXGI.ResultCode.DeviceReset||code==SharpDX.DXGI.ResultCode.NotCurrentlyAvailable||code==SharpDX.DXGI.ResultCode.SessionDisconnected;
        }
        // Zoom eases toward its target, about a fifth of a second to settle, instead of jumping.
        internal static double Approach(double shown,double target,double seconds)
        {
            double next=shown+(target-shown)*(1-Math.Exp(-seconds/.055));
            return Math.Abs(target-next)<.004?target:next;
        }
        void Run()
        {
            int failures=0;
            while(!stopping)
            {
                try
                {
                    Session();
                    if(stopping)break;
                    // The display layout changed; capture the new one.
                    failures=0;
                    Thread.Sleep(60);
                }
                catch(Exception error)
                {
                    if(stopping)break;
                    if(Recoverable(error)&&++failures<=60){Thread.Sleep(100);continue;}
                    if(Failed!=null)Failed("GPU display effect stopped: "+error.Message);
                    break;
                }
            }
        }
        void Session()
        {
            bool cursorApi=false,cursorHidden=false;
            try
            {
                // The color effect that is on the display is read every frame, which needs the display tools running whether or not the pointer is hidden.
                cursorApi=MagInitialize();
                if(!cursorApi)throw new InvalidOperationException("Windows could not start its display tools for zoom.");
                using(var host=new DisplayWindow())
                using(var factory=new Factory1())
                {
                    if(ReferenceEquals(Target,firstTarget)||ReferenceEquals(Target,followed)){followed=Forms.Screen.PrimaryScreen;Target=followed;}
                    var bounds=Target.Bounds;
                    host.Bounds=bounds;
                    OverlayHandle=host.Handle;
                    if(!AllowCapture&&!SetWindowDisplayAffinity(host.Handle,0x11))
                        throw new InvalidOperationException("Windows could not exclude the GPU overlay from desktop capture.");
                    Adapter1 adapter=null;
                    Output output=null;
                    foreach(var candidate in factory.Adapters1)
                    {
                        foreach(var display in candidate.Outputs)
                        {
                            var rectangle=display.Description.DesktopBounds;
                            if(rectangle.Left==bounds.Left&&rectangle.Top==bounds.Top&&rectangle.Right==bounds.Right&&rectangle.Bottom==bounds.Bottom)
                            {adapter=candidate;output=display;break;}
                            display.Dispose();
                        }
                        if(adapter!=null)break;
                        candidate.Dispose();
                    }
                    if(adapter==null)throw new DisplayNotReady("The primary display is unavailable for GPU capture.");
                    // A display turned on its side is captured in its own native orientation, so the picture is turned the other way when drawn.
                    int rotation=Math.Max(0,Math.Min(3,(int)output.Description.Rotation-1));
                    using(adapter)
                    using(output)
                    using(var device=new Device(adapter,DeviceCreationFlags.BgraSupport))
                    using(var output1=output.QueryInterface<Output1>())
                    using(var capture=output1.DuplicateOutput(device))
                    using(var renderer=new ZoomRenderer(device,factory,host.Handle,bounds.Width,bounds.Height,rotation))
                    {
                        bool visible=false,haveFrame=false;
                        double shown=1;
                        var clock=Stopwatch.StartNew();
                        double last=clock.Elapsed.TotalSeconds;
                        var look=new ZoomLook();
                        while(!stopping)
                        {
                            Forms.Application.DoEvents();
                            if(Live().Bounds!=bounds)return;
                            var wanted=request;
                            double now=clock.Elapsed.TotalSeconds,elapsed=Math.Min(.1,now-last);
                            last=now;
                            double target=Math.Max(1,Math.Min(4,wanted.Zoom));
                            shown=Approach(shown,target,elapsed);
                            bool zoomedIn=shown>1.001;
                            if(!zoomedIn&&!wanted.Wanted)
                            {
                                if(visible){host.Hide();visible=false;}
                                if(cursorHidden&&MagShowSystemCursor(true))cursorHidden=false;
                                Thread.Sleep(16);
                                continue;
                            }
                            // Centered zoom hides the pointer, which would otherwise sit at its unmagnified place. Following the pointer keeps it accurate.
                            bool hide=zoomedIn&&wanted.HideCursor&&!wanted.Follow;
                            if(hide&&!cursorHidden)
                            {
                                if(!MagShowSystemCursor(false))
                                    throw new InvalidOperationException("Windows could not hide the pointer for zoom.");
                                cursorHidden=true;
                            }
                            else if(!hide&&cursorHidden&&MagShowSystemCursor(true))cursorHidden=false;
                            // Take a newer desktop picture if there is one; otherwise keep drawing the last, so easing and pointer motion stay smooth.
                            OutputDuplicateFrameInformation info;
                            SharpDX.DXGI.Resource resource;
                            var result=capture.TryAcquireNextFrame(0,out info,out resource);
                            if(result!=SharpDX.DXGI.ResultCode.WaitTimeout)
                            {
                                result.CheckError();
                                try
                                {
                                    using(resource)using(var texture=resource.QueryInterface<Texture2D>())renderer.Capture(texture);
                                    haveFrame=true;
                                }
                                finally{capture.ReleaseFrame();}
                            }
                            if(!haveFrame){Thread.Sleep(2);continue;}
                            var matrix=new float[25];
                            if(!NativeEffects.MagGetFullscreenColorEffect(matrix))
                                throw new InvalidOperationException("Windows could not read the active display colors.");
                            look.Zoom=(float)shown;
                            look.Sharpen=(float)Math.Max(0,Math.Min(1,wanted.Sharpness));look.Adaptive=wanted.Adaptive;
                            look.Lift=(float)Math.Max(0,Math.Min(1,wanted.Lift));
                            look.Black=(float)Math.Max(0,Math.Min(1,wanted.Black));
                            look.PopHue=(float)wanted.PopHue;look.PopStrength=(float)Math.Max(0,Math.Min(1,wanted.PopStrength));
                            look.Tint=wanted.Tint;
                            if(wanted.Follow)
                            {
                                var pointer=Forms.Cursor.Position;
                                look.FocusX=(float)Math.Max(0,Math.Min(1,(pointer.X-bounds.Left)/(double)bounds.Width));
                                look.FocusY=(float)Math.Max(0,Math.Min(1,(pointer.Y-bounds.Top)/(double)bounds.Height));
                            }
                            else look.FocusX=look.FocusY=.5f;
                            renderer.SampleSpots=SamplePoints;
                            renderer.Present(look,matrix);
                            if(SamplePoints!=null)Sampled=renderer.Sampled;
                            Interlocked.Increment(ref FramesDrawn);
                            if(!visible){host.ShowOnTop();visible=true;}
                            else if(host.Revealing)host.FrameDrawn();
                        }
                    }
                }
            }
            finally
            {
                if(cursorHidden)MagShowSystemCursor(true);
                if(cursorApi)MagUninitialize();
            }
        }
    }

    internal sealed class ZoomRenderer : IDisposable
    {
        readonly Device device;
        readonly DeviceContext context;
        readonly SwapChain swap;
        readonly Texture2D current;
        readonly ShaderResourceView currentView;
        readonly RenderTargetView target;
        readonly VertexShader vertex;
        readonly PixelShader zoomShader;
        readonly SamplerState sampler;
        readonly SharpDX.Direct3D11.Buffer parameters;
        readonly int width,height,rotation;
        float[] colorMatrix,correction;
        Texture2D offscreen,backBuffer,staging;
        RenderTargetView offscreenTarget;
        internal int[][] SampleSpots,Sampled;

        // With a window the renderer presents to it. With IntPtr.Zero it only draws to a texture it owns, which is how it is tested.
        public ZoomRenderer(Device device,Factory1 factory,IntPtr window,int width,int height):this(device,factory,window,width,height,0){}
        // width and height are the desktop picture as it appears. rotation is 0 to 3 quarter turns (90 degrees each, clockwise) that the display is turned,
        // in which case the captured picture is height wide and width tall.
        public ZoomRenderer(Device device,Factory1 factory,IntPtr window,int width,int height,int rotation)
        {
            this.device=device;context=device.ImmediateContext;
            this.width=width;this.height=height;this.rotation=rotation;
            int sourceWidth=rotation%2==1?height:width,sourceHeight=rotation%2==1?width:height;
            if(window!=IntPtr.Zero)
            {
                swap=new SwapChain(factory,device,new SwapChainDescription {
                    BufferCount=2,ModeDescription=new ModeDescription(width,height,new Rational(0,1),Format.B8G8R8A8_UNorm),
                    IsWindowed=true,OutputHandle=window,SampleDescription=new SampleDescription(1,0),
                    Usage=Usage.RenderTargetOutput,SwapEffect=SwapEffect.Discard
                });
                factory.MakeWindowAssociation(window,WindowAssociationFlags.IgnoreAll);
                backBuffer=swap.GetBackBuffer<Texture2D>(0);
                target=new RenderTargetView(device,backBuffer);
            }
            current=Texture(sourceWidth,sourceHeight,Format.B8G8R8A8_UNorm,BindFlags.ShaderResource);
            currentView=new ShaderResourceView(device,current);
            using(var code=ShaderBytecode.Compile(Shader,"VS","vs_4_0"))vertex=new VertexShader(device,code);
            using(var code=ShaderBytecode.Compile(Shader,"Zoom","ps_4_0"))zoomShader=new PixelShader(device,code);
            sampler=new SamplerState(device,new SamplerStateDescription {
                Filter=Filter.MinMagMipLinear,AddressU=TextureAddressMode.Clamp,AddressV=TextureAddressMode.Clamp,
                AddressW=TextureAddressMode.Clamp,MaximumLod=float.MaxValue
            });
            parameters=new SharpDX.Direct3D11.Buffer(device,160,ResourceUsage.Default,BindFlags.ConstantBuffer,CpuAccessFlags.None,ResourceOptionFlags.None,0);
        }
        Texture2D Texture(int w,int h,Format format,BindFlags bind)
        {
            return new Texture2D(device,new Texture2DDescription {Width=w,Height=h,MipLevels=1,ArraySize=1,
                Format=format,SampleDescription=new SampleDescription(1,0),Usage=ResourceUsage.Default,BindFlags=bind});
        }
        // Keeps the newest desktop picture. It is drawn again each frame, so easing and pointer-following do not wait for the screen to change.
        public void Capture(Texture2D source)
        {
            var size=source.Description;var expected=current.Description;
            if(size.Width!=expected.Width||size.Height!=expected.Height)throw new InvalidOperationException("The captured picture ("+size.Width+" x "+size.Height+") is not the size of the display ("+expected.Width+" x "+expected.Height+").");
            context.PixelShader.SetShaderResources(0,new ShaderResourceView[3]);
            context.CopyResource(source,current);
        }
        public void Render(Texture2D source,float zoom,float[] activeColors)
        {
            Capture(source);
            Present(new ZoomLook {Zoom=zoom},activeColors);
        }
        public void Present(ZoomLook look,float[] activeColors)
        {
            Draw(look,activeColors,target);
            if(SampleSpots!=null&&backBuffer!=null)Sample(backBuffer);
            if(swap!=null)swap.Present(1,PresentFlags.None);
            context.PixelShader.SetShaderResources(0,new ShaderResourceView[3]);
        }
        // Reads a few pixels of the frame just drawn, which is how the real overlay is checked while it stays hidden from screen capture.
        void Sample(Texture2D drawn)
        {
            if(staging==null)staging=new Texture2D(device,new Texture2DDescription{Width=width,Height=height,MipLevels=1,ArraySize=1,Format=Format.B8G8R8A8_UNorm,SampleDescription=new SampleDescription(1,0),Usage=ResourceUsage.Staging,CpuAccessFlags=CpuAccessFlags.Read,BindFlags=BindFlags.None});
            context.CopyResource(drawn,staging);
            DataStream stream;var box=context.MapSubresource(staging,0,MapMode.Read,SharpDX.Direct3D11.MapFlags.None,out stream);
            var read=new int[SampleSpots.Length][];
            for(int i=0;i<read.Length;i++)
            {
                var px=new byte[4];stream.Position=(long)SampleSpots[i][1]*box.RowPitch+SampleSpots[i][0]*4L;stream.Read(px,0,4);
                read[i]=new int[]{px[2],px[1],px[0]};
            }
            context.UnmapSubresource(staging,0);stream.Dispose();
            Sampled=read;
        }
        // Draws one frame into a texture instead of the window.
        internal Texture2D RenderOffscreen(ZoomLook look,float[] activeColors)
        {
            if(offscreen==null)
            {
                offscreen=Texture(width,height,Format.B8G8R8A8_UNorm,BindFlags.RenderTarget|BindFlags.ShaderResource);
                offscreenTarget=new RenderTargetView(device,offscreen);
            }
            Draw(look,activeColors,offscreenTarget);
            context.PixelShader.SetShaderResources(0,new ShaderResourceView[3]);
            return offscreen;
        }
        void Draw(ZoomLook look,float[] activeColors,RenderTargetView view)
        {
            if(!ColorMath.Equal(colorMatrix,activeColors))
            {
                colorMatrix=(float[])activeColors.Clone();
                correction=ZoomColorCompensation.Build(colorMatrix);
            }
            var values=new float[40];
            values[0]=1f/width;values[1]=1f/height;values[2]=look.Sharpen;values[3]=look.Zoom;
            Array.Copy(correction,0,values,4,correction.Length);
            values[28]=look.FocusX;values[29]=look.FocusY;values[30]=look.Lift;values[31]=look.Black;
            values[32]=look.Tint[0];values[33]=look.Tint[1];values[34]=look.Tint[2];values[35]=rotation;
            values[36]=look.PopHue;values[37]=look.PopStrength;values[38]=look.Adaptive?1f:0f;
            context.UpdateSubresource(values,parameters);
            context.InputAssembler.PrimitiveTopology=PrimitiveTopology.TriangleList;
            context.VertexShader.Set(vertex);
            context.PixelShader.SetConstantBuffer(0,parameters);
            context.PixelShader.SetSampler(0,sampler);
            context.OutputMerger.SetRenderTargets(view);
            context.Rasterizer.SetViewport(0,0,width,height);
            context.PixelShader.SetShaderResources(0,currentView);
            context.PixelShader.Set(zoomShader);
            context.Draw(3,0);
        }
        public void Dispose()
        {
            context.ClearState();
            parameters.Dispose();sampler.Dispose();zoomShader.Dispose();vertex.Dispose();
            if(offscreenTarget!=null)offscreenTarget.Dispose();
            if(offscreen!=null)offscreen.Dispose();
            if(target!=null)target.Dispose();
            if(staging!=null)staging.Dispose();
            if(backBuffer!=null)backBuffer.Dispose();
            currentView.Dispose();current.Dispose();
            if(swap!=null)swap.Dispose();
            context.Dispose();
        }
        const string Shader=@"
Texture2D nowFrame:register(t0);
SamplerState linearSampler:register(s0);
cbuffer Settings:register(b0){float2 pixel;float sharpen;float zoom;float4 forward0,forward1,forward2,inverse0,inverse1,inverse2;float2 focus;float lift;float black;float4 tint;float2 pop;float2 spare;}
struct V {float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
V VS(uint id:SV_VertexID){
    V o;o.uv=float2((id<<1)&2,id&2);
    o.position=float4(o.uv*float2(2,-2)+float2(-1,1),0,1);return o;
}
// Where a point of the desktop as it appears is in the captured picture, which is in the display's own orientation.
float2 toSource(float2 d){
    int turns=(int)(tint.w+.5);
    if(turns==1)return float2(d.y,1-d.x);
    if(turns==2)return float2(1-d.x,1-d.y);
    if(turns==3)return float2(1-d.y,d.x);
    return d;
}
float3 hsvOf(float3 c){
    float4 k=float4(0,-1.0/3.0,2.0/3.0,-1);
    float4 p=lerp(float4(c.bg,k.wz),float4(c.gb,k.xy),step(c.b,c.g));
    float4 q=lerp(float4(p.xyw,c.r),float4(c.r,p.yzx),step(p.x,c.r));
    float d=q.x-min(q.w,q.y);
    return float3(abs(q.z+(q.w-q.y)/(6*d+1e-10)),d/(q.x+1e-10),q.x);
}
float3 mapped(float3 c){
    return float3(dot(forward0.xyz,c)+forward0.w,dot(forward1.xyz,c)+forward1.w,dot(forward2.xyz,c)+forward2.w);
}
float4 Zoom(V input):SV_TARGET{
    float magnify=max(1,zoom);
    // The point at the focus stays where it is; everything else spreads out from it. The sampled spot always stays on the screen.
    float2 at=(input.uv-focus)/magnify+focus;
    float3 desired=nowFrame.SampleLevel(linearSampler,toSource(at),0).rgb;
    if(sharpen>0){
        // Stretching pixels softens the picture, so pull edges back in, one output pixel wide and with a cap so lines do not ring.
        float2 step=pixel/magnify;
        float3 east=nowFrame.SampleLevel(linearSampler,toSource(at+float2(step.x,0)),0).rgb,west=nowFrame.SampleLevel(linearSampler,toSource(at-float2(step.x,0)),0).rgb;
        float3 south=nowFrame.SampleLevel(linearSampler,toSource(at+float2(0,step.y)),0).rgb,north=nowFrame.SampleLevel(linearSampler,toSource(at-float2(0,step.y)),0).rgb;
        float3 around=(east+west+south+north)*.25;
        float3 detail=desired-around;
        float amount=sharpen*2.4;
        if(spare.x>.5){
            // Adaptive: how much the picture changes within a pixel on each side decides how much to sharpen. Differences too small to be an edge are
            // noise and are left alone, edges that are already strong are eased off so they do not ring, and the further it is zoomed in, the more it
            // was softened, so the more it is pulled back.
            // Measured a whole pixel of the original picture away, so the answer is the same wherever the pixel falls along an edge.
            float3 e2=nowFrame.SampleLevel(linearSampler,toSource(at+float2(pixel.x,0)),0).rgb,w2=nowFrame.SampleLevel(linearSampler,toSource(at-float2(pixel.x,0)),0).rgb;
            float3 s2=nowFrame.SampleLevel(linearSampler,toSource(at+float2(0,pixel.y)),0).rgb,n2=nowFrame.SampleLevel(linearSampler,toSource(at-float2(0,pixel.y)),0).rgb;
            float3 high=max(max(max(e2,w2),max(s2,n2)),desired),low=min(min(min(e2,w2),min(s2,n2)),desired);
            float3 range=high-low;
            float contrast=max(range.r,max(range.g,range.b));
            float weight=smoothstep(.012,.05,contrast)*(1-smoothstep(.22,.5,contrast));
            amount*=weight*(.7+.3*saturate((magnify-1)/2));
        }        desired=saturate(desired+clamp(detail*amount,-.16,.16));
    }
    if(pop.y>0&&pop.x>=0){
        // The chosen color is made richer and brighter; everything else is calmed toward gray, so the one color is what the eye finds.
        float3 hsv=hsvOf(desired);
        float gap=abs(hsv.x-pop.x);gap=min(gap,1-gap);
        float match=(1-smoothstep(.04,.10,gap))*smoothstep(.15,.35,hsv.y)*smoothstep(.10,.28,hsv.z);
        float gray=dot(desired,float3(.2126,.7152,.0722));
        float3 others=lerp(desired,(gray*.92).xxx,pop.y*.65*(1-match));
        float3 boosted=saturate(lerp(gray.xxx,desired,1+pop.y*1.1)*(1+pop.y*.45));
        desired=lerp(others,boosted,match);
    }
    if(lift>0){
        // A gamma curve raises the shadows a lot and the highlights hardly at all, so a dark corner opens up without the sky going white.
        desired=pow(max(desired,.0001),(1/(1+lift*1.8)).xxx);
        float floorLevel=black*.2;
        desired=saturate((desired-floorLevel)/(1-floorLevel))*tint.rgb;
    }
    float gray=max(desired.r,max(desired.g,desired.b));
    float3 residual=desired-mapped(gray.xxx);
    float3 value=saturate(gray.xxx+float3(dot(inverse0.xyz,residual),dot(inverse1.xyz,residual),dot(inverse2.xyz,residual)));
    [unroll]for(int i=0;i<12;i++){
        float3 error=saturate(mapped(value))-desired;
        float3 gradient=forward0.xyz*error.r+forward1.xyz*error.g+forward2.xyz*error.b;
        value=saturate(value-inverse0.w*gradient);
    }
    return float4(value,1);
}
";
    }
}
