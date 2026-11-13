using System;
using System.IO;
using System.Linq;
using Lucent;
using ResolutionSwitcher;
internal static class LogicTests
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static float[] Transform(float[] matrix,float r,float g,float b){float[] source={r,g,b,1,1},result=new float[5];for(int i=0;i<5;i++)for(int j=0;j<5;j++)result[j]+=source[i]*matrix[i*5+j];return result;}
    static void Main()
    {
        Check(ColorMath.Equal(ColorMath.Build(new Look()),ColorMath.Identity()),"Neutral color is not identity");
        var oldLook = new Look { Saturation = 0 }; Check(oldLook.Saturation == 100 && ColorMath.Equal(ColorMath.Build(oldLook), ColorMath.Identity()), "Legacy sub-neutral vibrance must become neutral");
        var red=Transform(ColorMath.Build(new Look {Red=100}),0,0,1);Check(red[0]>.01&&red[1]==0&&red[2]==0,"Full red filter must retain blue input as red luminance");
        var dim=Transform(ColorMath.Build(new Look {Dim=50}),1,1,1);Check(Math.Abs(dim[0]-.5)<.0001&&Math.Abs(dim[1]-.5)<.0001&&Math.Abs(dim[2]-.5)<.0001,"Dimming changed channels unequally");
        // Smart shadows are drawn by the GPU, so the display matrix must not brighten a second time. Fast Dark Boost still does.
        var fast=ColorMath.Build(new Look {DarkEnabled=true,DarkStrength=60,DarkColor="White"});var smart=ColorMath.Build(new Look {DarkEnabled=true,DarkSmart=true,DarkStrength=60,DarkColor="Green"});
        Check(!ColorMath.Equal(fast,ColorMath.Identity())&&ColorMath.Equal(smart,ColorMath.Identity()),"Smart shadows and the display matrix must not both lift the picture");
        var fresh=new Look();Check(!fresh.DarkSmart&&!fresh.ZoomFollow&&fresh.ZoomSharpness==30,"New look defaults keep Dark Boost fast and zoom centered");
        var cleanPrefs=new Preferences();cleanPrefs.Desktop.DarkColor="Clean";cleanPrefs.Profiles.Add(new GameProfile {Look=new Look {DarkColor="Clean"}});StateStore.Normalize(cleanPrefs);Check(cleanPrefs.Desktop.DarkColor=="White"&&cleanPrefs.Profiles[0].Look.DarkColor=="White","A saved Clean tint should become White, which is the same thing");
        var zoomOld=new Preferences {HoldMouse5Zoom=true,ZoomHoldKey=6,ZoomKey=120};StateStore.Normalize(zoomOld);var zoomOldToggle=new Preferences {HoldMouse5Zoom=false,ZoomKey=120};StateStore.Normalize(zoomOldToggle);
        Check(zoomOld.ZoomBind==6&&zoomOldToggle.ZoomBind==120&&zoomOld.ZoomBindMigrated&&new Look().ZoomSharp=="Adaptive","Older zoom keys should become the one zoom bind, and sharpening should start adaptive");
        var zoomAgain=new Preferences {ZoomBind=0,ZoomBindMigrated=true,ZoomKey=120};StateStore.Normalize(zoomAgain);Check(zoomAgain.ZoomBind==0,"A cleared zoom bind must stay cleared");
        var zoomFollow=new Preferences();zoomFollow.Desktop.ZoomFollow=true;zoomFollow.Desktop.ZoomSharp="Weird";StateStore.Normalize(zoomFollow);Check(!zoomFollow.Desktop.ZoomFollow&&zoomFollow.Desktop.ZoomSharp=="Adaptive","Zoom around the pointer is gone and an unknown sharpening mode becomes adaptive");
        var lowZoom=new Preferences();lowZoom.Desktop.Zoom=1;StateStore.Normalize(lowZoom);Check(lowZoom.Desktop.Zoom==Look.MinimumZoom,"A saved zoom of 1x should be raised to the minimum");
        var oddPrefs=new Preferences();oddPrefs.Desktop.ZoomSharpness=900;oddPrefs.Desktop.Zoom=99;StateStore.Normalize(oddPrefs);Check(oddPrefs.Desktop.ZoomSharpness==100&&oddPrefs.Desktop.Zoom==4,"Saved zoom values are pulled back into range");
        var visionCopy=oddPrefs.Desktop.Copy();visionCopy.DarkSmart=true;visionCopy.ZoomFollow=true;Check(visionCopy.DarkSmart&&visionCopy.ZoomFollow&&!oddPrefs.Desktop.DarkSmart,"Profile copies keep the new switches independently");
        var strong=ColorMath.Build(new Look {Saturation=300,DarkEnabled=true,DarkStrength=100});Check(strong.All(x=>!float.IsNaN(x)&&!float.IsInfinity(x)),"High strength produced invalid matrix");
        // The red filter follows a glowing black body: cream, then amber, then orange, then red. Blue goes first, green last, never pink.
        Func<double,float[]> warm=t=>Transform(ColorMath.Build(new Look {Red=t}),1,1,1);
        float lastGreen=1.01f,lastBlue=1.01f;
        for(int t=5;t<=100;t+=5){var c=warm(t);Check(c[0]>=.99f&&c[1]<=lastGreen+1e-4f&&c[2]<=lastBlue+1e-4f&&c[2]<=c[1]+1e-4f,"Red filter is not warming steadily at "+t+"%: "+c[0]+","+c[1]+","+c[2]);lastGreen=c[1];lastBlue=c[2];}
        Check(warm(15)[1]>.7f&&warm(15)[2]>.45f&&warm(15)[2]<.7f,"Low strength should be a cream yellow: "+warm(15)[1]+","+warm(15)[2]);
        Check(warm(50)[1]>.35f&&warm(50)[1]<.6f&&warm(50)[2]<.05f,"Half strength should be orange: "+warm(50)[1]+","+warm(50)[2]);
        Check(warm(100)[1]<.05f&&warm(100)[2]<.01f,"Full strength should be red");        // Color blindness names: the old ones still load and mean the same thing.
        Check(ColorMath.Equal(ColorMath.Build(new Look {Vision="Red-weak"}),ColorMath.Build(new Look {Vision="Protanopia"}))&&!ColorMath.Equal(ColorMath.Build(new Look {Vision="Protanopia"}),ColorMath.Identity()),"Protanopia");
        Check(ColorMath.Equal(ColorMath.Build(new Look {Vision="Green-weak"}),ColorMath.Build(new Look {Vision="Deuteranopia"}))&&ColorMath.Equal(ColorMath.Build(new Look {Vision="Blue-weak"}),ColorMath.Build(new Look {Vision="Tritanopia"})),"Deuteranopia or Tritanopia");
        var migrated=new Preferences();migrated.Desktop.Vision="Green-weak";migrated.Profiles.Add(new GameProfile {Look=new Look {Vision="Blue-weak"}});StateStore.Normalize(migrated);Check(migrated.Desktop.Vision=="Deuteranopia"&&migrated.Profiles[0].Look.Vision=="Tritanopia","Saved vision names were not migrated");
        Check(Art.Glyph("Video")!=Art.Glyph("Resolution"),"Video and Resolution share an icon");
        var look=new Look {Red=20};var copy=look.Copy();copy.Red=80;Check(look.Red==20,"Profile look shares mutable state");
        StateStore.Folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data","logic");var prefs=new Preferences();prefs.Profiles.Add(new GameProfile {Name="game",Executable="C:\\games\\game.exe",Look=copy});StateStore.Save(prefs);var loaded=StateStore.Load();Check(loaded.Profiles.Count==1&&loaded.Profiles[0].Look.Red==80&&loaded.Desktop.Red==0,"Profile persistence failed");
        StateStore.Save(loaded);Check(File.Exists(StateStore.Config+".bak"),"Atomic preferences did not preserve backup");
        // Crosshairs: drawn pixel for pixel, shaped by presets, shared by code, and migrated from older settings.
        Func<CrosshairSettings,int,System.Drawing.Bitmap> draw=(spec,pad)=>{int side=CrosshairRenderer.Extent(spec)*2+1+pad*2;var bitmap=new System.Drawing.Bitmap(side,side);using(var g=System.Drawing.Graphics.FromImage(bitmap)){g.Clear(System.Drawing.Color.Magenta);CrosshairRenderer.Draw(g,new System.Drawing.Rectangle(0,0,side,side),spec);}return bitmap;};
        Func<System.Drawing.Bitmap,System.Drawing.Color,int> count=(bitmap,color)=>{int n=0;for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++)if(bitmap.GetPixel(x,y).ToArgb()==color.ToArgb())n++;return n;};
        var white=System.Drawing.Color.FromArgb(255,255,255);var black=System.Drawing.Color.FromArgb(0,0,0);var key=System.Drawing.Color.Magenta;
        var plain=CrosshairPresets.Create("Cross");plain.Outline=false;
        using(var bitmap=draw(plain,2))Check(count(bitmap,white)==60,"A cross of length 8 and thickness 2 should be 60 pixels: "+count(bitmap,white));
        var dotLook=CrosshairPresets.Create("Dot");dotLook.Outline=false;using(var bitmap=draw(dotLook,2))Check(count(bitmap,white)==12,"A 4 pixel dot should be 12 pixels: "+count(bitmap,white));
        var squareLook=CrosshairPresets.Create("Square");squareLook.Outline=false;using(var bitmap=draw(squareLook,2))Check(count(bitmap,white)==36,"A 6 pixel square should be 36 pixels");
        // Only the two colors ever appear, and nothing is drawn at the very edge of the crosshair's own extent.
        foreach(string name in CrosshairPresets.Names)
        {
            var preset=CrosshairPresets.Create(name);preset.OutlineThickness=3;
            using(var bitmap=draw(preset,0))
            {
                int side=bitmap.Width;
                for(int y=0;y<side;y++)for(int x=0;x<side;x++)
                {
                    int c=bitmap.GetPixel(x,y).ToArgb();bool edge=x==0||y==0||x==side-1||y==side-1;
                    Check(c==white.ToArgb()||c==black.ToArgb()||c==key.ToArgb(),name+" blended a color");
                    Check(!edge||c==key.ToArgb(),name+" draws outside its own extent");
                }
                Check(count(bitmap,white)>0&&count(bitmap,black)>0,name+" has no fill or no outline");
            }
        }
        // The ring is gone: an old look that had one loads without it, and nothing ever draws one.
        var oldRing=CrosshairPresets.Create("Gap cross");oldRing.Ring=true;oldRing.Clamp();Check(!oldRing.Ring&&!CrosshairPresets.Names.Contains("Ring")&&CrosshairPresets.Create("Ring").Name=="Gap cross","The ring should be removed");
        // One shared middle: with an even thickness the arms and the dot sit on the line between two pixels, with an odd one on a pixel, and everything is mirror-symmetric around it.
        Func<CrosshairSettings,bool> symmetrical=spec=>
        {
            int extent=CrosshairRenderer.Extent(spec);var fill=CrosshairRenderer.Fill(spec,extent);bool even=CrosshairRenderer.EvenCenter(spec);bool ok=true;
            for(int y=-extent;y<=extent;y++)for(int x=-extent;x<=extent;x++)
            {
                int mx=even?-1-x:-x,my=even?-1-y:-y;
                if(mx<-extent||mx>extent||my<-extent||my>extent)continue;
                if(fill[x+extent,y+extent]!=fill[mx+extent,y+extent]||fill[x+extent,y+extent]!=fill[x+extent,my+extent])ok=false;
            }
            return ok;
        };
        foreach(int thick in new[]{1,2,3,4,5})foreach(int dot in new[]{0,1,2,3,4,5})
        {
            var spec=CrosshairPresets.Create("Cross");spec.Thickness=thick;spec.Length=9;spec.Gap=3;spec.Dot=dot>0;spec.DotSize=Math.Max(1,dot);spec.SquareDot=dot%2==1;
            Check(symmetrical(spec),"A crosshair with thickness "+thick+" and dot "+dot+" is not centered on one middle");
        }
        var dotOnly=CrosshairPresets.Create("Dot");foreach(int size in new[]{1,2,3,4,5,6})foreach(bool square in new[]{false,true}){dotOnly.DotSize=size;dotOnly.SquareDot=square;Check(symmetrical(dotOnly),"A dot of "+size+" is not centered");}
        var mixed=CrosshairPresets.Create("Cross");mixed.Thickness=2;mixed.Dot=true;mixed.DotSize=3;Check(CrosshairRenderer.EvenCenter(mixed)&&CrosshairRenderer.DotPixels(mixed)==4,"A dot beside even arms should follow them to an even size");
        // On an even-sized screen an even crosshair is drawn across the true middle line, two pixels wide around it.
        var twoWide=CrosshairPresets.Create("Cross");twoWide.Outline=false;twoWide.Thickness=2;twoWide.Length=4;twoWide.Gap=0;
        using(var screen=new System.Drawing.Bitmap(40,40))using(var g=System.Drawing.Graphics.FromImage(screen)){g.Clear(key);CrosshairRenderer.Draw(g,new System.Drawing.Rectangle(0,0,40,40),twoWide);
            Func<int,int,bool> on=(x,y)=>screen.GetPixel(x,y).ToArgb()!=key.ToArgb();
            Check(on(19,17)&&on(20,17)&&!on(18,17)&&!on(21,17)&&on(19,22)&&on(20,22)&&on(17,19)&&on(17,20)&&!on(17,18)&&!on(17,21)&&on(16,19)&&on(23,20)&&!on(15,19)&&!on(24,20),"A two pixel cross should straddle the middle line of an even screen, the same length on every side");}
        var xLook=CrosshairPresets.Create("X");xLook.Outline=false;
        using(var bitmap=draw(xLook,0)){int middle=(bitmap.Width-1)/2;Check(bitmap.GetPixel(middle+5,middle+5).ToArgb()==white.ToArgb()&&bitmap.GetPixel(middle-5,middle+5).ToArgb()==white.ToArgb()&&bitmap.GetPixel(middle+8,middle).ToArgb()!=white.ToArgb(),"X arms are not on the diagonals");}
        var offsetLook=CrosshairPresets.Create("Dot");offsetLook.Outline=false;offsetLook.DotSize=1;offsetLook.OffsetX=5;offsetLook.OffsetY=-3;
        using(var bitmap=new System.Drawing.Bitmap(41,41))using(var g=System.Drawing.Graphics.FromImage(bitmap)){g.Clear(key);CrosshairRenderer.Draw(g,new System.Drawing.Rectangle(0,0,41,41),offsetLook);Check(bitmap.GetPixel(25,17).ToArgb()==white.ToArgb()&&count(bitmap,white)==1,"Offset did not move the crosshair");}
        Check(CrosshairPresets.Names.Length==6&&!CrosshairPresets.Names.Contains("positted")&&CrosshairPresets.Names.Select(n=>{var l=CrosshairPresets.Create(n);l.Name="x";return CrosshairCode.Encode(l);}).Distinct().Count()==6,"The styles are not six different shapes");
        var custom=CrosshairPresets.Create("T");custom.Name="My look";custom.Length=12;custom.Gap=5;custom.Thickness=3;custom.Rotation=20;custom.Opacity=70;custom.OffsetX=-4;custom.OffsetY=6;custom.FillColor="#19E5A7";custom.OutlineColor="#102030";custom.OutlineThickness=2;custom.Dot=true;custom.SquareDot=true;custom.DotSize=5;
        var decoded=CrosshairCode.Decode(CrosshairCode.Encode(custom));
        Check(decoded!=null&&CrosshairCode.Encode(decoded)==CrosshairCode.Encode(custom)&&decoded.Name=="My look"&&decoded.Left&&!decoded.Top&&decoded.FillColor=="#19E5A7","Crosshair code round trip");
        {
            // X and Y apart: the top and bottom arms take their own length and gap; codes carry it, and older codes load linked.
            var split=CrosshairPresets.Create("Cross");split.Name="split";split.Rotation=0;split.Dot=false;split.Length=10;split.Gap=2;split.Linked=false;split.LengthY=4;split.GapY=6;split.Thickness=1;split.Clamp();
            int ext=CrosshairRenderer.Extent(split);var mask=CrosshairRenderer.Fill(split,ext);
            Check(mask[ext+11,ext]&&!mask[ext+12,ext],"The X arms should reach 2+10 pixels");
            Check(mask[ext,ext+9]&&!mask[ext,ext+10]&&!mask[ext,ext+5],"The Y arms should run from gap 6 for 4 pixels");
            var splitBack=CrosshairCode.Decode(CrosshairCode.Encode(split));
            Check(splitBack!=null&&!splitBack.Linked&&splitBack.LengthY==4&&splitBack.GapY==6&&splitBack.VerticalLength==4,"The X/Y split did not survive a code");
            split.Linked=true;Check(split.VerticalLength==10&&split.VerticalGap==2,"Linked should use one length and gap");
            string body="Old;1111;7;2;4;00;3;0;10;2;0;100;0;0;#FFFFFF;1;#000000;1";
            var old=CrosshairCode.Decode("LCX1-"+Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(body)));
            Check(old!=null&&old.Linked&&old.VerticalLength==7,"An older code should still load, linked");
            Console.WriteLine("PASS: crosshair X and Y can be set apart (own length and gap for the top and bottom arms), carried in codes; older codes load linked.");
        }
        Check(CrosshairCode.Decode("")==null&&CrosshairCode.Decode("hello")==null&&CrosshairCode.Decode("LCX1-!!!")==null&&CrosshairCode.Decode("LCX1-"+Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("a;b")))==null,"Bad codes must be refused");
        var wild=custom.Copy();wild.Length=999;wild.Opacity=-5;wild.FillColor="nonsense";var tamed=CrosshairCode.Decode(CrosshairCode.Encode(wild));Check(tamed!=null&&tamed.Length==40&&tamed.Opacity==20&&tamed.FillColor=="#FFFFFF","A code with wild values was not tamed");
        Func<string,Preferences> loadXml=xml=>{var p=(Preferences)new System.Xml.Serialization.XmlSerializer(typeof(Preferences)).Deserialize(new StringReader(xml));StateStore.Normalize(p);return p;};
        var fromOld=loadXml("<Preferences><CrosshairShape>positted</CrosshairShape><CrosshairColor>#FF19E5A7</CrosshairColor><CrosshairOutline>false</CrosshairOutline></Preferences>");
        Check(fromOld.Crosshair.Name=="Gap cross"&&fromOld.Crosshair.FillColor=="#19E5A7"&&!fromOld.Crosshair.Outline,"The removed style should become the gap cross, with its color kept");
        var classic=loadXml("<Preferences><CrosshairShape>Classic</CrosshairShape><CrosshairSize>0.6</CrosshairSize></Preferences>").Crosshair;Check(classic.Name=="Cross"&&classic.Length==4&&classic.Thickness==1&&classic.Gap==0,"Old classic crosshair");
        Check(loadXml("<Preferences><CrosshairShape>Dot</CrosshairShape></Preferences>").Crosshair.Dot&&loadXml("<Preferences><CrosshairShape>Square</CrosshairShape></Preferences>").Crosshair.SquareDot&&loadXml("<Preferences/>").Crosshair.Name=="Gap cross","Old dot, square and defaults");
        var kept=new Preferences();kept.Crosshair=custom.Copy();kept.SavedCrosshairs.Add(CrosshairPresets.Create("Square"));StateStore.Save(kept);var back=StateStore.Load();
        Check(CrosshairCode.Encode(back.Crosshair)==CrosshairCode.Encode(custom)&&back.SavedCrosshairs.Count==1&&back.SavedCrosshairs[0].SquareDot,"Crosshair persistence");
        Check(BackgroundAppBoost.IsProtectedProcessName("VALORANT-Win64-Shipping")&&BackgroundAppBoost.IsProtectedProcessName("MsMpEng")&&BackgroundAppBoost.IsProtectedProcessName("lucent"),"Process protections missing");
        var optional=BackgroundAppBoost.Catalog.First(d=>d.Id=="medal");var riot=new ProcessIdentity {Name=optional.ProcessNames[0],Path="C:\\Riot Games\\test.exe",Company="Riot Games"};Check(!BackgroundAppBoost.CanManage(optional,riot),"Riot-owned helper is manageable");
        using(var colors=new EffectsClient()){} // Construction/disposal must not start native effects.
        // Sound radar: speaker levels become directions, quiet sound stays dark, and the arcs are drawn where the sound is. Nothing here touches audio.
        var leftOnly=SoundDirections.Map(new[]{1f,0f});Check(leftOnly[6]==1&&leftOnly[2]==0&&leftOnly[0]==0&&leftOnly[7]>.4,"A sound in the left speaker should light the left side");
        var rightOnly=SoundDirections.Map(new[]{0f,.8f});Check(Math.Abs(rightOnly[2]-.8)<.001&&rightOnly[6]==0,"A sound in the right speaker should light the right side");
        var evenStereo=SoundDirections.Map(new[]{.5f,.5f});Check(Math.Abs(evenStereo[7]-evenStereo[1])<.001&&Math.Abs(evenStereo[6]-evenStereo[2])<.001,"Equal left and right should be mirror images");
        var center=SoundDirections.Map(new float[]{0,0,1,0,0,0});Check(center[0]==1&&center[3]==0&&center[5]==0,"The center speaker of 5.1 is straight ahead");
        Check(SoundDirections.Map(new float[]{0,0,0,1,0,0}).All(v=>v==0),"The bass channel of 5.1 has no direction");
        var behindLeft=SoundDirections.Map(new float[]{0,0,0,0,1,0});Check(behindLeft[5]==1&&behindLeft[3]==0&&behindLeft[4]>0,"The rear left speaker is behind and to the left");
        var sideRight=SoundDirections.Map(new float[]{0,0,0,0,0,0,0,1});Check(sideRight[2]==1&&sideRight[6]==0,"The right side speaker of 7.1 is to the right");
        Check(SoundDirections.Map(null).All(v=>v==0)&&SoundDirections.Map(new float[0]).All(v=>v==0)&&SoundDirections.Map(new float[]{1,1,1}).Length==SoundDirections.Sectors,"Odd channel counts must not fail");
        Check(SoundDirections.Shape(.01,50)==0&&SoundDirections.Shape(1,50)>.99&&SoundDirections.Shape(.1,90)>SoundDirections.Shape(.1,10)&&SoundDirections.Shape(.5,50)>SoundDirections.Shape(.3,50),"Sensitivity decides how quiet a sound can be and still show");
        var screenArea=new System.Drawing.Rectangle(0,0,1920,1080);
        Check(SoundRadar.Place(screenArea,"Center",240)==new System.Drawing.Point(840,420)&&SoundRadar.Place(screenArea,"Top",240)==new System.Drawing.Point(840,40)&&SoundRadar.Place(screenArea,"Bottom",240)==new System.Drawing.Point(840,690),"The radar is placed in the middle, top or bottom of the screen");
        Check(new RadarOptions{Size="Small"}.Pixels==170&&new RadarOptions{Size="Medium"}.Pixels==240&&new RadarOptions{Size="Large"}.Pixels==320,"Radar sizes");
        using(var quiet=RadarRenderer.Render(new double[8],240))
        {
            bool empty=true;for(int y=0;y<240&&empty;y++)for(int x=0;x<240;x++)if(quiet.GetPixel(x,y).A!=0){empty=false;break;}
            Check(empty,"A quiet radar must draw nothing");
        }
        using(var loud=RadarRenderer.Render(new double[]{0,0,1,0,0,0,0,0},240))
        {
            Check(loud.GetPixel(225,120).A>200&&loud.GetPixel(14,120).A==0&&loud.GetPixel(120,14).A==0&&loud.GetPixel(120,225).A==0,"A sound on the right should draw only the right arc");
        }
        using(var soft=RadarRenderer.Render(new double[]{0,0,.15,0,0,0,0,0},240))using(var hard=RadarRenderer.Render(new double[]{0,0,1,0,0,0,0,0},240))
            Check(soft.GetPixel(225,120).A<hard.GetPixel(225,120).A,"A louder sound should be brighter");
        var fresh2=new Look();Check(fresh2.PopColor=="Off"&&!fresh2.RadarOn&&fresh2.RadarSize=="Medium"&&fresh2.RadarPlace=="Center"&&fresh2.PopStrength==60&&fresh2.RadarSensitivity==60,"New look defaults keep Color pop and the radar off");
        var strange=new Preferences();strange.Desktop.PopColor="Purple";strange.Desktop.RadarSize="Huge";strange.Desktop.RadarPlace="Nowhere";strange.Desktop.PopStrength=500;strange.Desktop.RadarSensitivity=-9;StateStore.Normalize(strange);
        Check(strange.Desktop.PopColor=="Off"&&strange.Desktop.RadarSize=="Medium"&&strange.Desktop.RadarPlace=="Center"&&strange.Desktop.PopStrength==100&&strange.Desktop.RadarSensitivity==0,"Saved Color pop and radar values are pulled back into range");
        Check(ColorPop.HueFor("Off")==-1&&ColorPop.HueFor("Red")==0&&Math.Abs(ColorPop.HueFor("Green")-1/3d)<.001&&ColorPop.HueFor("nonsense")==-1,"Color pop hues");        // Updates: reading versions, choosing where to ask, reading what comes back, and never opening anything but a secure address. No request is made.
        Check(Updates.ParseVersion("0.6.27").Equals(new Version(0,6,27,0))&&Updates.ParseVersion("v1.2").Equals(new Version(1,2,0,0))&&Updates.ParseVersion("Lucent 2.0.1 (beta)").Equals(new Version(2,0,1,0))&&Updates.ParseVersion("latest")==null&&Updates.ParseVersion("")==null&&Updates.ParseVersion(null)==null,"Version text is not read correctly");
        Check(Updates.FeedUrl("owner/lucent")=="https://api.github.com/repos/owner/lucent/releases/latest"&&Updates.FeedUrl(" https://example.com/lucent.json ")=="https://example.com/lucent.json"&&Updates.FeedUrl("http://example.com/x.json")==null&&Updates.FeedUrl("file:///C:/x")==null&&Updates.FeedUrl("owner/repo/extra")==null&&Updates.FeedUrl("")==null&&Updates.FeedUrl("rm -rf /")==null,"Only an owner/repository or an https address may be checked");
        var release=Updates.Parse("{\"tag_name\":\"v0.7.0\",\"html_url\":\"https://github.com/owner/lucent/releases/tag/v0.7.0\",\"body\":\"New home page\\r\\nFaster zoom\",\"draft\":false,\"prerelease\":false}");
        Check(release!=null&&release.Latest.Equals(new Version(0,7,0,0))&&release.Url.EndsWith("v0.7.0")&&release.Notes.Contains("Faster zoom")&&!release.Notes.Contains("\r"),"A GitHub release was not read");
        var plainFile=Updates.Parse("{\"version\":\"0.8.1\",\"url\":\"https://example.com/get\",\"notes\":\"Fixes\"}");Check(plainFile!=null&&plainFile.Latest.Equals(new Version(0,8,1,0))&&plainFile.Url=="https://example.com/get","A plain version file was not read");
        Check(Updates.Parse("{\"tag_name\":\"v9.0.0\",\"prerelease\":true}")==null&&Updates.Parse("{\"tag_name\":\"v9.0.0\",\"draft\":true}")==null&&Updates.Parse("not json")==null&&Updates.Parse("{\"tag_name\":\"nightly\"}")==null&&Updates.Parse("[]")==null,"A draft, a pre-release or nonsense must not count as an update");
        Check(Updates.Parse("{\"version\":\"1.0\",\"notes\":\""+new string('x',900)+"\"}").Notes.Length==900&&Updates.Parse("{\"version\":\"1.0\",\"notes\":\""+new string('x',9000)+"\"}").Notes.Length<=8001,"Release notes should be kept whole, and only a runaway text cut");
        Check(Updates.NotesPreview("a\nb")=="a\nb"&&Updates.NotesPreview(string.Join("\n",new string[12]))!=string.Join("\n",new string[12])&&Updates.NotesPreview(new string('y',900)).Length<=521,"The notes preview should be the start of long notes");
        // Release names: 1.MMYY, with .2 to .99 for more builds in a month; the year counts before the month when comparing.
        Check(Updates.ReleaseName(new DateTime(2026,11,6),1)=="1.1126"&&Updates.ReleaseName(new DateTime(2027,2,14),1)=="1.0227"&&Updates.ReleaseName(new DateTime(2026,11,20),3)=="1.1126.3"&&Updates.ReleaseName(new DateTime(2026,11,20),150)=="1.1126.99","Release names do not follow 1.MMYY");
        Check(Updates.Show(new Version(1,1126,0,0))=="1.1126"&&Updates.Show(new Version(1,227,0,0))=="1.0227"&&Updates.Show(new Version(1,1126,3,0))=="1.1126.3"&&Updates.Show(new Version(0,6,30,0))=="0.6.30","Versions are not written the way people read them");
        Check(Updates.ParseVersion("1.0227").Equals(new Version(1,227,0,0))&&Updates.ParseVersion("v1.1126.3").Equals(new Version(1,1126,3,0)),"A month-and-year version was not read");
        string plainNotes=Updates.PlainNotes("## What's new\n\n### A fresh look\n- **Liquid glass** bar, see [the page](https://x)\n\n\n\n* `code` item");
        Check(plainNotes=="What's new\n\nA fresh look\n• Liquid glass bar, see the page\n\n• code item","Release notes should show as plain text: "+plainNotes);
        Check(Updates.Compare(new Version(1,127,0,0),new Version(1,1226,0,0))>0&&Updates.Compare(new Version(1,1126,2,0),new Version(1,1126,0,0))>0&&Updates.Compare(new Version(1,1226,0,0),new Version(1,1126,99,0))>0&&Updates.Compare(new Version(1,1126,0,0),new Version(0,6,30,0))>0&&Updates.Compare(new Version(0,6,30,0),new Version(0,6,4,0))>0&&Updates.Compare(new Version(1,1126,3,0),new Version(1,1126,3,0))==0,"Versions are not put in the right order");
        Check(Updates.IsNewer(new UpdateInfo{Latest=new Version(1,127,0,0)},new Version(1,1226,0,0))&&!Updates.IsNewer(new UpdateInfo{Latest=new Version(1,1126,0,0)},new Version(1,1226,0,0)),"January 2027 must count as newer than December 2026");
        Check(UsageStats.Duration(5)=="5 min"&&UsageStats.Duration(192)=="3 h 12 min"&&UsageStats.Duration(60*24*2+60*5)=="2 d 5 h"&&UsageStats.Duration(-4)=="0 min","Times are not written the way people read them");
        var brokenStats=new Preferences();brokenStats.Stats=null;StateStore.Normalize(brokenStats);var negative=new Preferences();negative.Stats.Launches=-3;negative.Stats.Since="";StateStore.Normalize(negative);
        Check(brokenStats.Stats!=null&&negative.Stats.Launches==0&&negative.Stats.Since.Length==10,"Saved stats are not repaired");
        Check(SystemStats.CleanRings(null)==SystemStats.DefaultRings&&SystemStats.CleanRings("Memory, CPU ,Nope,cpu temp")=="CPU,CPU temp,RAM"&&SystemStats.CleanRings("CPU,Memory,GPU")=="CPU,RAM,GPU"&&SystemStats.CleanRings("")==""&&SystemStats.CleanRings("GPU,GPU,Video memory")=="GPU,Video memory","The chosen circles are not cleaned up");
        // The processor temperature helper: the link reads what it serves (a made-up 57.5 here, no hardware), says -1 without it, and asks for it to be started.
        string sensorPipe="lucent-test-"+Guid.NewGuid().ToString("N");
        string helperExe=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"sensors","LucentSensors.test.exe");
        if(File.Exists(helperExe))
        {
            var helper=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(helperExe,"--fake 57.5 --parent "+System.Diagnostics.Process.GetCurrentProcess().Id+" --pipe "+sensorPipe){CreateNoWindow=true,UseShellExecute=false});
            try
            {
                var link=new SensorLink {Pipe=sensorPipe,ForceAvailable=true,Launch=()=>true};double got=-1;
                for(int i=0;i<80&&got<0;i++){got=link.Read();System.Threading.Thread.Sleep(100);}
                link.Dispose();
                Check(Math.Abs(got-57.5)<.01,"The sensor link did not read what the helper served: "+got);
            }
            finally{try{helper.Kill();}catch(Exception){}}
        }
        else Console.WriteLine("NOTE: the sensor helper is not built, so its link was not tested.");
        int launches=0;var lonely=new SensorLink {Pipe="lucent-test-nobody-"+Guid.NewGuid().ToString("N"),ForceAvailable=true,Launch=()=>{launches++;return true;}};
        lonely.Read();for(int i=0;i<60&&launches==0;i++)System.Threading.Thread.Sleep(100);
        Check(lonely.Read()==-1&&launches>=1,"With no helper the link should say -1 and ask for it to be started: "+launches);lonely.Dispose();
        Check(SensorLink.Installed==(File.Exists(Path.Combine(SensorLink.HelperFolder,"task.ok"))&&File.Exists(Path.Combine(SensorLink.HelperFolder,"LucentSensors.exe"))),"The helper counts as installed only when setup made its task and left its file");
        var current=new Version(0,6,26,0);Check(Updates.IsNewer(release,current)&&!Updates.IsNewer(new UpdateInfo{Latest=current},current)&&!Updates.IsNewer(new UpdateInfo{Latest=new Version(0,6,25,0)},current)&&!Updates.IsNewer(null,current),"Only a higher version is an update");
        Check(Updates.CanOpen("https://github.com/o/r/releases")&&!Updates.CanOpen("http://x.com")&&!Updates.CanOpen("javascript:alert(1)")&&!Updates.CanOpen("file:///C:/Windows/notepad.exe")&&!Updates.CanOpen("")&&!Updates.CanOpen(null),"Only a secure web address may be opened from an update notice");
        string asked=null;Updates.Fetcher=url=>{asked=url;return "{\"tag_name\":\"v3.0.0\",\"html_url\":\"https://example.com/r\"}";};
        var checkedInfo=Updates.Check("owner/repo");Check(asked=="https://api.github.com/repos/owner/repo/releases/latest"&&checkedInfo.Latest.Major==3,"Checking asked the wrong place or misread the answer");
        bool refused=false;try{Updates.Check("ftp://nope");}catch(InvalidOperationException){refused=true;}Check(refused&&asked=="https://api.github.com/repos/owner/repo/releases/latest","An address Lucent cannot check must be refused before any request");
        Updates.Fetcher=url=>"{}";refused=false;try{Updates.Check("owner/repo");}catch(InvalidOperationException){refused=true;}Check(refused,"An answer without a version must be reported");
        var defaults=new Preferences();Check(defaults.CloseToTray&&!defaults.TrayHintShown&&defaults.CheckUpdates&&defaults.UpdateSource==""&&defaults.NotifiedVersion=="","Close to tray and update checks should default on, with no source set yet");
        Check(new GameProfile().Launch=="","A game added by hand has no store address");
        // Settings from before the name change are copied to the new folder once, and the old folder is left as it was.
        var oldFolder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data","old-name"+Environment.TickCount);var newFolder=oldFolder+"-copy";
        Directory.CreateDirectory(Path.Combine(oldFolder,"backgrounds"));File.WriteAllText(Path.Combine(oldFolder,"preferences.xml"),"saved");File.WriteAllText(Path.Combine(oldFolder,"backgrounds","a.txt"),"picture");
        StateStore.CopyFolder(oldFolder,newFolder);
        Check(File.ReadAllText(Path.Combine(newFolder,"preferences.xml"))=="saved"&&File.ReadAllText(Path.Combine(newFolder,"backgrounds","a.txt"))=="picture"&&File.Exists(Path.Combine(oldFolder,"preferences.xml")),"Settings were not carried over to the renamed folder");
        Directory.Delete(oldFolder,true);Directory.Delete(newFolder,true);
        Check(LegacyNames.SingleInstanceMutex.StartsWith("Local\\")&&LegacyNames.DataFolder.Length>0,"The names shared with installed versions are missing");
        DiscordChecks();
        DiscordHelperChecks();
        LauncherChecks();
        HomeLayoutChecks();
        VisionChecks();
        Console.WriteLine("PASS: neutral/legacy-range/red-only/dim matrices, black-body red filter, crosshair rendering/styles/codes/migration, finite high-strength matrix, distinct icons, independent profile snapshots, atomic persistence and backup, system/Vanguard/game/lucent process protections and verified publisher checks, lazy effects lifecycle. No live color/audio/display writes were invoked.");
        string fixtureFolder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data","ram-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixtureFolder);
        var start=new System.Diagnostics.ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"lucent.ramfixture.exe"),"\""+fixtureFolder+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden};
        using(var fixture=System.Diagnostics.Process.Start(start))
        {
            try{var timeout=System.Diagnostics.Stopwatch.StartNew();while(!File.Exists(Path.Combine(fixtureFolder,"ready.txt"))&&timeout.ElapsedMilliseconds<5000)System.Threading.Thread.Sleep(25);Check(File.Exists(Path.Combine(fixtureFolder,"ready.txt")),"RAM fixture did not start");
                var memory = RamCleanup.ReadMemory(); Check(memory.Total > 0 && memory.Available <= memory.Total, "Invalid physical RAM reading");
                string result=RamCleanup.Run(new[]{new BoostAppDefinition("ram-fixture","Test fixture","lucent.ramfixture","lucent.ramfixture")}, false);Check(result.StartsWith("Trimmed 1 background processes"),"RAM cleanup did not trim the fixture: "+result);Console.WriteLine("PASS: isolated RAM fixture: "+result);
            }finally{File.WriteAllText(Path.Combine(fixtureFolder,"stop.txt"),"stop");Check(fixture.WaitForExit(5000),"RAM fixture did not stop normally");}
        }
    }

    // ---- Discord Rich Presence: what is built for a song, and the real client talking to a made-up Discord (a pipe this test makes). The real Discord is never touched.
    static byte[] ReadFrame(System.IO.Stream s,out int kind)
    {
        var head=new byte[8];int got=0;while(got<8){int n=s.Read(head,got,8-got);if(n<=0)throw new System.IO.EndOfStreamException();got+=n;}
        kind=BitConverter.ToInt32(head,0);var body=new byte[BitConverter.ToInt32(head,4)];got=0;while(got<body.Length){int n=s.Read(body,got,body.Length-got);if(n<=0)throw new System.IO.EndOfStreamException();got+=n;}
        return body;
    }
    // ---- Home layout: the standard layout, and a damaged or partial saved layout can never lose or double a card.
    static void HomeLayoutChecks()
    {
        var standard=HomeLayoutText.Parse("");
        Check(string.Join(",",standard.Select(e=>e.Key))=="display,playing,pc,shortcuts"&&standard.All(e=>!e.Full&&!e.Hidden),"The standard Home layout is wrong");
        var mine=HomeLayoutText.Parse("!pc:f;actions:h;shortcuts:h;display:f");
        Check(string.Join(",",mine.Select(e=>e.Key))=="pc,shortcuts,display,playing"&&mine[0].Hidden&&mine[0].Full&&!mine[1].Full&&mine[2].Full&&!mine[3].Hidden,"A saved Home layout was not read back in order: "+HomeLayoutText.Serialize(mine));
        Check(HomeLayoutText.Parse("pc:h;pc:f;nonsense:h;;;:x;display").Count==4&&HomeLayoutText.Parse("pc:h;pc:f;nonsense:h").Count(e=>e.Key=="pc")==1,"A damaged Home layout lost or doubled a card");
        Check(HomeLayoutText.Clean(HomeLayoutText.Serialize(mine))==HomeLayoutText.Serialize(mine)&&HomeLayoutText.Clean(null)==HomeLayoutText.Clean(""),"Cleaning a Home layout is not stable");
        var prefs=new Preferences{HomeLayout="garbage;;"};StateStore.Normalize(prefs);
        Check(prefs.HomeLayout==HomeLayoutText.Clean(""),"A broken Home layout was not put right when settings were read");
        Console.WriteLine("PASS: Home layout: standard order and widths, a saved order with hidden and full-width cards read back, damaged text cannot lose or double a card, and settings reading puts it right.");
    }
    // ---- Games from the other launchers: the records they leave behind are read correctly, and only safe launch addresses are ever opened.
    static void LauncherChecks()
    {
        Check(GameLibrary.RiotInstallFolder("product_install_full_path: \"C:\\\\Riot Games\\\\VALORANT\"\r\nother: 1")==@"C:\Riot Games\VALORANT","The Riot install folder was not read from its settings file");
        Check(GameLibrary.RiotInstallFolder("product_install_full_path: D:/Games/Riot Games/League of Legends\n")==@"D:\Games\Riot Games\League of Legends","A Riot install folder written with slashes was not read");
        Check(GameLibrary.RiotInstallFolder("nothing here")==null&&GameLibrary.RiotInstallFolder(null)==null,"A Riot settings file without a folder should give none");
        Check(GameLibrary.RiotName("valorant")=="VALORANT"&&GameLibrary.RiotName("league_of_legends")=="League of Legends"&&GameLibrary.RiotName("some_new_game")=="Some New Game","Riot product names are wrong");
        string name,exe;
        Check(GameLibrary.ParseXboxConfig("<Game configVersion=\"1\"><ShellVisuals DefaultDisplayName=\"Halo Infinite\"/><ExecutableList><Executable Name=\"HaloInfinite.exe\" Id=\"Game\"/></ExecutableList></Game>",out name,out exe)&&name=="Halo Infinite"&&exe=="HaloInfinite.exe","The Xbox game config was not read");
        Check(GameLibrary.ParseXboxConfig("<Game><ShellVisuals DefaultDisplayName=\"ms-resource:Title\"/><ExecutableList><Executable Name=\"bin/Game.exe\"/></ExecutableList></Game>",out name,out exe)&&name==null&&exe==@"bin\Game.exe","A resource-only Xbox name must be left out and the program kept");
        Check(!GameLibrary.ParseXboxConfig("not xml <<<",out name,out exe),"A broken Xbox config must not be accepted");
        Check(GameLibrary.IsStoreAddress("uplay://launch/635/0")&&!GameLibrary.IsStoreAddress("uplay://launch/635/0&x")&&!GameLibrary.IsStoreAddress("uplay://launch/abc/0")&&!GameLibrary.IsStoreAddress("goggalaxy://openGameView/1"),"Only a Ubisoft launch address of the exact form may be opened");
        var games=GameLibrary.Scan();
        Check(games!=null,"Scanning every launcher must always return a list, even when none is installed");
        Console.WriteLine("PASS: games from GOG, Ubisoft, EA, Battle.net, Riot and Xbox: Riot and Xbox records read (names, folders, programs), broken or resource-only records handled, only the exact Ubisoft launch address added, and a scan of this PC returned "+games.Count+" games without error.");
    }
    // ---- Discord must not call a tool a game: gh.exe (the GitHub CLI) is also listed for Green Hell, which lives in a folder named "Green Hell".
    static void DiscordHelperChecks()
    {
        DiscordGameIcons.Reset();
        DiscordGameIcons.Parse("[{\"id\":\"1\",\"name\":\"Green Hell\",\"executables\":[{\"name\":\"green hell/gh.exe\",\"os\":\"win32\"}]},{\"id\":\"2\",\"name\":\"Plain Game\",\"executables\":[{\"name\":\"plaingame.exe\",\"os\":\"win32\"}]}]");
        Check(DiscordGameIcons.FindRunning(new[]{"gh"},n=>@"C:\Program Files\GitHub CLI\gh.exe")==null,"The GitHub CLI was taken for Green Hell");
        var real=DiscordGameIcons.FindRunning(new[]{"gh"},n=>@"D:\Games\Green Hell\GH.exe");
        Check(real!=null&&real.Name=="Green Hell","Green Hell running from its own folder was not recognised");
        Check(DiscordGameIcons.FindRunning(new[]{"gh"},n=>null)!=null,"A game whose path cannot be read should still be recognised by name");
        var plain=DiscordGameIcons.FindRunning(new[]{"plaingame"},n=>@"E:\Anywhere\plaingame.exe");
        Check(plain!=null&&plain.Name=="Plain Game","A game listed without a folder should be recognised wherever it is");
        DiscordGameIcons.Reset();
        Console.WriteLine("PASS: a program that shares a file name with a game (gh.exe and Green Hell) is only taken for the game when it runs from the game's folder.");
        {
            // Games Discord does not list (Rob Run): a program running from an installed game's folder is that game.
            Check(InstalledGames.Folder(@"D:\SteamLibrary\steamapps\common\Rob Run\Rob Run.exe")==@"D:\SteamLibrary\steamapps\common\Rob Run","Steam game folder");
            Check(InstalledGames.Folder(@"C:\Steam\steamapps\common\Big Game\Binaries\Win64\Big.exe")==@"C:\Steam\steamapps\common\Big Game","A Steam game's own folder, not where its program sits");
            Check(InstalledGames.Folder(@"C:\Program Files\tool.exe")==null&&InstalledGames.Folder(@"C:\tool.exe")==null,"A shared folder is not a game's folder");
            var folders=new[]{new System.Collections.Generic.KeyValuePair<string,LibraryGame>(@"D:\SteamLibrary\steamapps\common\Rob Run",new LibraryGame {Name="Rob Run"})};
            var hit=InstalledGames.Match(folders,new[]{@"C:\Windows\explorer.exe",@"D:\SteamLibrary\steamapps\common\Rob Run\Rob Run.exe"});
            Check(hit!=null&&hit.Name=="Rob Run","Rob Run running from its folder was not recognised");
            Check(InstalledGames.Match(folders,new[]{@"D:\SteamLibrary\steamapps\common\Rob Run\UnityCrashHandler64.exe"})==null,"A crash handler is not the game");
            Check(InstalledGames.Match(folders,new[]{@"D:\SteamLibrary\steamapps\common\Rob Run Two\x.exe"})==null,"A folder that only starts with the same name is another game");
            Console.WriteLine("PASS: games Discord does not list (like Rob Run) are recognised by running from an installed game's folder; helpers and look-alike folders are not.");
        }
    }
    static void DiscordChecks()
    {
        var now=new DateTime(2026,11,6,12,0,0,DateTimeKind.Utc);
        long unix=(long)(now-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds*1000;
        var on=new PresenceOptions();
        var rust=new PresenceInfo{Game="Rust",GameIcon="https://cdn.discordapp.com/app-icons/1/abc.png",Since=now.AddMinutes(-101)};
        var activity=DiscordActivity.Build(rust,on,now);
        var stamps=(System.Collections.Generic.Dictionary<string,object>)activity["timestamps"];
        var assets=(System.Collections.Generic.Dictionary<string,object>)activity["assets"];
        Check((int)activity["type"]==0&&(string)activity["name"]=="Rust with Lucent"&&(string)activity["details"]=="Playing Rust"&&(string)activity["state"]=="with Lucent"&&(long)stamps["start"]==unix-101*60000,"The status for a game is wrong");
        Check((string)assets["large_image"]=="https://cdn.discordapp.com/app-icons/1/abc.png"&&(string)assets["small_image"]=="lucent"&&(string)assets["small_text"]=="Lucent","The game picture should be large with the Lucent mark small in its corner, like Medal");
        Check(((object[])activity["buttons"]).Length==1&&((System.Collections.Generic.Dictionary<string,object>)((object[])activity["buttons"])[0])["url"].ToString().StartsWith("https://github.com/"),"The Get Lucent button is wrong");
        var plain=DiscordActivity.Build(new PresenceInfo{Game="Rust",Since=now},new PresenceOptions{Timeline=false,Button=false},now);
        PlayHistory.Clear();PlayHistory.Add(new MediaTrack{Title="One",Artist="A",Source="Spotify.exe"});PlayHistory.Add(new MediaTrack{Title="One",Artist="A",Source="Spotify.exe"});PlayHistory.Add(new MediaTrack{Title="Video",Artist="B",Source="Chrome"});PlayHistory.Add(new MediaTrack{Title="Two",Artist="A",Source="Spotify.exe"});
        Check(PlayHistory.Newest().Count==2&&PlayHistory.Newest()[0].Title=="Two"&&PlayHistory.Newest()[1].Title=="One","Recently played keeps each song once, newest first, and leaves out what a browser plays");PlayHistory.Clear();
        Check(DiscordActivity.IsBrowserSource("Chrome")&&DiscordActivity.IsBrowserSource("MSEdge")&&DiscordActivity.IsBrowserSource("BraveSoftware.Brave-Browser")&&DiscordActivity.IsBrowserSource("Firefox-308046B0AF4A39CB")&&DiscordActivity.IsBrowserSource("Opera.GX")&&!DiscordActivity.IsBrowserSource("Spotify.exe")&&!DiscordActivity.IsBrowserSource("AppleInc.AppleMusicWin")&&!DiscordActivity.IsBrowserSource(null),"Media from web browsers must be told apart from music apps");
        Check((int)DiscordActivity.Build(new PresenceInfo{Game="Rust",Since=now},new PresenceOptions{Competing=true},now)["type"]==5&&(int)DiscordActivity.Build(new PresenceInfo{Game="Rust",Since=now},new PresenceOptions(),now)["type"]==0&&new Preferences().DiscordCompeting,"A game shows as Competing (beside the game's own card) when asked, Playing otherwise");
        var plainAssets=(System.Collections.Generic.Dictionary<string,object>)plain["assets"];
        Check(!plain.ContainsKey("timestamps")&&!plain.ContainsKey("buttons")&&(string)plainAssets["large_image"]=="lucent"&&!plainAssets.ContainsKey("small_image"),"Switches must be respected, and a game with no picture falls back to the Lucent logo");
        Check(DiscordActivity.Build(null,on,now)==null&&DiscordActivity.Build(new PresenceInfo{Game=" "},on,now)==null&&DiscordActivity.Build(new PresenceInfo(),on,now)==null,"With no game running there must be no status at all (no music, no Lucent on its own)");
        var idle=DiscordActivity.Build(new PresenceInfo{Since=now.AddMinutes(-5)},new PresenceOptions{Idle=true},now);
        var idleAssets=(System.Collections.Generic.Dictionary<string,object>)idle["assets"];
        Check(idle!=null&&(string)idle["name"]=="Lucent"&&(string)idle["details"]=="Idle"&&idle.ContainsKey("timestamps")&&idle.ContainsKey("buttons")&&(string)idleAssets["large_image"]=="lucent","The Idle status is wrong");
        Check(DiscordActivity.Build(new PresenceInfo(),new PresenceOptions{Idle=true,Timeline=false,Button=false},now).ContainsKey("timestamps")==false&&DiscordActivity.Build(new PresenceInfo(),new PresenceOptions{Idle=false},now)==null&&(string)DiscordActivity.Build(new PresenceInfo{Game="Rust"},new PresenceOptions{Idle=true},now)["details"]=="Playing Rust","Idle must only appear with no game, and only when switched on");
        Check(!idle.ContainsKey("state")&&(string)idle["details"]=="Idle","Idle should say only Idle");
        var songNow=new PresenceInfo{Title="Nvr again",Artist="Yeat",CoverUrl="https://e-cdns-images.dzcdn.net/images/cover/abc/500x500.jpg",Position=54,Duration=166};
        var listening=DiscordActivity.Build(songNow,new PresenceOptions{Songs=true,Idle=true},now);
        var listenStamps=(System.Collections.Generic.Dictionary<string,object>)listening["timestamps"];var listenAssets=(System.Collections.Generic.Dictionary<string,object>)listening["assets"];
        Check((int)listening["type"]==2&&(string)listening["name"]=="Lucent"&&(string)listening["details"]=="Nvr again"&&(string)listening["state"]=="Yeat","The song status is wrong");
        Check((long)listenStamps["start"]==unix-54*1000&&(long)listenStamps["end"]==unix-54*1000+166*1000,"The song's progress bar should run from its start to its end");
        Check((string)listenAssets["large_image"]==songNow.CoverUrl&&(string)listenAssets["small_image"]=="lucent"&&((object[])listening["buttons"]).Length==1&&!listening.ContainsKey("instance"),"The song should show its cover with the Lucent mark in the corner and only the Get Lucent button");
        var noCover=DiscordActivity.Build(new PresenceInfo{Title="Song",Artist="A",Duration=100},new PresenceOptions{Songs=true,Timeline=false,Button=false},now);
        Check(!noCover.ContainsKey("timestamps")&&!noCover.ContainsKey("buttons")&&(string)((System.Collections.Generic.Dictionary<string,object>)noCover["assets"])["large_image"]=="lucent","A song with no cover found should show the Lucent logo");
        Check((string)listenAssets["large_text"]=="Listening with Lucent"&&(string)((System.Collections.Generic.Dictionary<string,object>)noCover["assets"])["large_text"]=="Listening with Lucent","The line under the artist should say Listening with Lucent, with or without a cover");
        {
            // Deezer's exact search finds nothing for some songs ("Janice STFU"), so a plain search must follow it.
            var keptCover=SongCovers.SearchFetcher;SongCovers.Reset();var asked=new System.Collections.Generic.List<string>();
            SongCovers.SearchFetcher=query=>{asked.Add(query);return query.Contains("track:")?"{\"data\":[]}":"{\"data\":[{\"title\":\"Janice STFU\",\"artist\":{\"name\":\"Drake\"},\"album\":{\"cover_big\":\"https://cdn-images.dzcdn.net/images/cover/abc/500x500.jpg\"}}]}";};
            string janice=SongCovers.Find("Janice STFU","Drake");
            Check(janice!=null&&janice.Contains("/abc/")&&asked.Count==2,"A song the exact search misses should still get its cover from the plain search: "+janice);
            SongCovers.Reset();SongCovers.SearchFetcher=keptCover;
        }
        Check(DiscordActivity.Build(songNow,new PresenceOptions{Songs=false,Idle=true},now)["details"].ToString()=="Idle"&&DiscordActivity.Build(songNow,new PresenceOptions{Songs=false},now)==null,"With songs off a playing song must not be shown");
        Check((string)DiscordActivity.Build(new PresenceInfo{Game="Rust",Title="Nvr again",Duration=100},new PresenceOptions{Songs=true},now)["details"]=="Playing Rust","A running game comes before a song");
        Check(new Preferences().DiscordSongs,"Show songs should be on by default (Discord itself is off until switched on)");
        // The cover comes from Deezer's answer: the result with the same title and artist, from Deezer's own servers.
        string deezer="{\"data\":[{\"title\":\"Other\",\"artist\":{\"name\":\"Yeat\"},\"album\":{\"cover_big\":\"https://e-cdns-images.dzcdn.net/images/cover/wrong/500x500.jpg\"}},{\"title\":\"Nvr again\",\"artist\":{\"name\":\"Yeat\"},\"album\":{\"cover_big\":\"https://e-cdns-images.dzcdn.net/images/cover/right/500x500.jpg\"}}]}";
        Check(SongCovers.CoverUrl(deezer,"Nvr again (feat. X)","Yeat, Someone").Contains("/right/")&&SongCovers.CoverUrl(deezer,"Nvr again","Drake")==null&&SongCovers.CoverUrl(deezer.Replace("e-cdns-images.dzcdn.net","evil.example.com"),"Nvr again","Yeat")==null&&SongCovers.CoverUrl("not json","a","b")==null,"The song cover must be of the same song and from Deezer");        Check(new Preferences().DiscordIdle,"Show Idle should be on by default");
        Check(DiscordActivity.Line("X").Length>=2&&DiscordActivity.Line(new string('a',300)).Length==128,"Lines must be 2 to 128 characters");
        // Pictures come from Discord's own list of known games.
        DiscordGameIcons.Reset();
        DiscordGameIcons.Parse("[{\"id\":\"356869127241072640\",\"name\":\"Rust\",\"icon\":\"hash1\",\"executables\":[{\"os\":\"win32\",\"name\":\"rustclient.exe\"},{\"os\":\"linux\",\"name\":\"rust\"}]},{\"id\":\"2\",\"name\":\"Other Game\",\"icon\":null,\"executables\":[{\"os\":\"win32\",\"name\":\"other.exe\"}]}]");
        Check(DiscordGameIcons.Lookup(@"C:\Games\Rust\RustClient.exe","Anything").Icon=="https://cdn.discordapp.com/app-icons/356869127241072640/hash1.png?size=512"&&DiscordGameIcons.Lookup(@"D:\x\game.exe","rust").Name=="Rust"&&DiscordGameIcons.Lookup(@"D:\x\other.exe","Other Game").Icon==null&&DiscordGameIcons.Lookup(@"D:\x\unknown.exe","Unknown")==null,"A game picture was not found the way Discord lists it");
        // A game is recognised from the names of running programs alone, like Discord does it: no game profile is needed.
        DiscordGameIcons.Parse("[{\"id\":\"3\",\"name\":\"ROBLOX\",\"aliases\":[\"Roblox\"],\"icon_hash\":\"hash3\",\"executables\":[{\"os\":\"win32\",\"name\":\">RobloxPlayerBeta.exe\"},{\"os\":\"win32\",\"name\":\"RobloxLauncher.exe\",\"is_launcher\":true}]},{\"id\":\"1\",\"name\":\"Rust\",\"icon\":\"hash1\",\"executables\":[{\"os\":\"win32\",\"name\":\"RustClient.exe\"}]}]");
        var roblox=DiscordGameIcons.FindRunning(new[]{"explorer","chrome","RobloxPlayerBeta","RustClient"});
        Check(roblox!=null&&roblox.Name=="Roblox"&&roblox.Icon.EndsWith("/3/hash3.png?size=512")&&DiscordGameIcons.FindRunning(new[]{"explorer","RobloxLauncher","notepad"})==null&&DiscordGameIcons.FindRunning(new string[0])==null&&DiscordGameIcons.FindRunning(null)==null,"A running game was not recognised from its program name, or a launcher counted as a game");
        // A helper that many games share (the Unity crash handler) must not name the game: it made Phasmophobia show up as Spellcraft.
        DiscordGameIcons.Parse("[{\"id\":\"7\",\"name\":\"Spellcraft\",\"icon_hash\":\"h7\",\"executables\":[{\"os\":\"win32\",\"name\":\"spellcraft dev (staging)/unitycrashhandler64.exe\"},{\"os\":\"win32\",\"name\":\"spellcraft-live.exe\"}]},{\"id\":\"8\",\"name\":\"Phasmophobia\",\"icon_hash\":\"h8\",\"executables\":[{\"os\":\"win32\",\"name\":\"Phasmophobia.exe\"}]}]");
        var crashShared=DiscordGameIcons.FindRunning(new[]{"UnityCrashHandler64","Phasmophobia"});
        Check(crashShared!=null&&crashShared.Name=="Phasmophobia"&&DiscordGameIcons.FindRunning(new[]{"UnityCrashHandler64"})==null&&DiscordGameIcons.FindRunning(new[]{"spellcraft-live"}).Name=="Spellcraft","A shared helper program (the Unity crash handler) was taken for a game");
        DiscordGameIcons.Parse("[{\"id\":\"3\",\"name\":\"ROBLOX\",\"aliases\":[\"Roblox\"],\"icon_hash\":\"hash3\",\"executables\":[{\"os\":\"win32\",\"name\":\">RobloxPlayerBeta.exe\"},{\"os\":\"win32\",\"name\":\"RobloxLauncher.exe\",\"is_launcher\":true}]},{\"id\":\"1\",\"name\":\"Rust\",\"icon\":\"hash1\",\"executables\":[{\"os\":\"win32\",\"name\":\"RustClient.exe\"}]}]");
        // The real client against a made-up Discord.
        string prefix="lucent-test-discord-"+Guid.NewGuid().ToString("N")+"-";
        var received=new System.Collections.Concurrent.ConcurrentQueue<string>();string seenClient=null;
        var server=new System.IO.Pipes.NamedPipeServerStream(prefix+"0",System.IO.Pipes.PipeDirection.InOut,1,System.IO.Pipes.PipeTransmissionMode.Byte,System.IO.Pipes.PipeOptions.None);
        var thread=new System.Threading.Thread(()=>
        {
            try
            {
                server.WaitForConnection();
                int kind;string hello=System.Text.Encoding.UTF8.GetString(ReadFrame(server,out kind));seenClient=hello;
                var ready=DiscordRpcClient.Frame(1,"{\"cmd\":\"DISPATCH\",\"evt\":\"READY\",\"data\":{\"user\":{\"username\":\"tester\"}}}");server.Write(ready,0,ready.Length);
                while(true)
                {
                    string command=System.Text.Encoding.UTF8.GetString(ReadFrame(server,out kind));received.Enqueue(command);
                    var answer=DiscordRpcClient.Frame(1,"{\"cmd\":\"SET_ACTIVITY\",\"evt\":null,\"data\":{}}");server.Write(answer,0,answer.Length);
                }
            }
            catch(Exception){}
        }){IsBackground=true};
        thread.Start();
        var client=new DiscordRpcClient{ClientId="123456789",PipePrefix=prefix,MinimumGap=50,RefreshEvery=60000};
        client.Start();
        for(int i=0;i<100&&!client.Status.StartsWith("Connected");i++)System.Threading.Thread.Sleep(50);
        Check(client.Status=="Connected as tester"&&seenClient!=null&&seenClient.Contains("\"client_id\":\"123456789\"")&&seenClient.Contains("\"v\":1"),"The client did not say hello to Discord properly: "+client.Status+" "+seenClient);
        System.Threading.Thread.Sleep(400);string drained;while(received.TryDequeue(out drained)){}   // on connecting it sends one clear, in case an older status was left
        client.Set(DiscordActivity.Build(rust,on,now));
        string sent;for(int i=0;i<100&&!received.TryPeek(out sent);i++)System.Threading.Thread.Sleep(50);
        received.TryDequeue(out sent);
        Check(sent!=null&&sent.Contains("\"cmd\":\"SET_ACTIVITY\"")&&sent.Contains("\"name\":\"Rust with Lucent\"")&&sent.Contains("\"details\":\"Playing Rust\"")&&sent.Contains("\"type\":0")&&sent.Contains("\"pid\":"),"Discord did not get the game: "+sent);
        client.Set(null);
        for(int i=0;i<100&&!received.TryPeek(out sent);i++)System.Threading.Thread.Sleep(50);
        received.TryDequeue(out sent);
        Check(sent!=null&&sent.Contains("\"activity\":null"),"Clearing the status did not reach Discord: "+sent);
        client.Set(DiscordActivity.Build(rust,on,now));
        for(int i=0;i<100&&!received.TryPeek(out sent);i++)System.Threading.Thread.Sleep(50);
        received.TryDequeue(out sent);
        client.Dispose();
        Check(client.Status=="Disabled","Stopping the client should leave it disabled");
        string last=null;string item;while(received.TryDequeue(out item))last=item;
        Check(last!=null&&last.Contains("\"activity\":null"),"Stopping must clear the status from Discord: "+last);
        try{server.Dispose();}catch(Exception){}
        // No Discord at all.
        var alone=new DiscordRpcClient{ClientId="123",PipePrefix="lucent-test-nobody-"+Guid.NewGuid().ToString("N")+"-",RetryEvery=100};
        alone.Start();alone.Set(DiscordActivity.Build(rust,on,now));
        for(int i=0;i<60&&alone.Status!="Discord is not running";i++)System.Threading.Thread.Sleep(50);
        Check(alone.Status=="Discord is not running","Without Discord the client should say so: "+alone.Status);
        alone.Dispose();
        var unset=new DiscordRpcClient{ClientId=""};unset.Start();
        Check(unset.Status=="Needs setup","Without a Discord application the client should say it is not set up");
        Check(new Preferences().DiscordOn&&new Preferences().DiscordTimeline&&new Preferences().DiscordButton,"Discord Rich Presence must be on by default");var oldPrefs=new Preferences{DiscordOn=false};StateStore.Normalize(oldPrefs);Check(oldPrefs.DiscordOn&&oldPrefs.DiscordSettingVersion==1,"Settings saved before the default changed must be turned on once");oldPrefs.DiscordOn=false;StateStore.Normalize(oldPrefs);Check(!oldPrefs.DiscordOn,"After that, switching it off must be respected");
        Console.WriteLine("PASS: Discord status built for a running game (and nothing when none runs), game pictures found from Discord's list, and the client said hello, sent, cleared and stopped against a made-up Discord. The real Discord was not touched.");
    }
    // ---- Color blindness modes: against a simulation of each deficiency (Machado 2009), the filter must make colors that look alike to that viewer clearly different.
    static double LinC(double c){c/=255.0;return c<=0.04045?c/12.92:Math.Pow((c+0.055)/1.055,2.4);}
    static double GamC(double l){l=Math.Max(0,Math.Min(1,l));return 255.0*(l<=0.0031308?12.92*l:1.055*Math.Pow(l,1/2.4)-0.055);}
    static double[] SeenBy(double[] rgb,double[,] sim)
    {
        var l=new[]{LinC(rgb[0]),LinC(rgb[1]),LinC(rgb[2])};var o=new double[3];
        for(int i=0;i<3;i++)o[i]=GamC(sim[i,0]*l[0]+sim[i,1]*l[1]+sim[i,2]*l[2]);return o;
    }
    static double[] LabOf(double[] rgb)
    {
        double r=LinC(rgb[0]),g=LinC(rgb[1]),b=LinC(rgb[2]);
        double x=(0.4124*r+0.3576*g+0.1805*b)/0.95047,y=0.2126*r+0.7152*g+0.0722*b,z=(0.0193*r+0.1192*g+0.9505*b)/1.08883;
        Func<double,double> f=t=>t>0.008856?Math.Pow(t,1.0/3):7.787*t+16.0/116;
        return new[]{116*f(y)-16,500*(f(x)-f(y)),200*(f(y)-f(z))};
    }
    static double Delta(double[] a,double[] b){var p=LabOf(a);var q=LabOf(b);return Math.Sqrt((p[0]-q[0])*(p[0]-q[0])+(p[1]-q[1])*(p[1]-q[1])+(p[2]-q[2])*(p[2]-q[2]));}
    // What the display shows after the color matrix (pixel values as on screen, clipped).
    static double[] Shown(float[] screen,double[] rgb)
    {
        var o=new double[3];
        for(int j=0;j<3;j++){double s=0;for(int i=0;i<3;i++)s+=rgb[i]*screen[i*5+j];o[j]=Math.Max(0,Math.Min(255,s));}
        return o;
    }
    static void VisionChecks()
    {
        var models=new System.Collections.Generic.Dictionary<string,double[,]>
        {
            {"Protanopia",new double[,]{{0.152286,1.052583,-0.204868},{0.114503,0.786281,0.099216},{-0.003882,-0.048116,1.051998}}},
            {"Deuteranopia",new double[,]{{0.367322,0.860646,-0.227968},{0.280085,0.672501,0.047413},{-0.011820,0.042940,0.968881}}},
            {"Tritanopia",new double[,]{{1.255528,-0.076749,-0.178779},{-0.078411,0.930809,0.147602},{0.004733,0.691367,0.303900}}}
        };
        Check(ColorMath.Equal(ColorMath.Build(new Look {Vision="Normal"}),ColorMath.Identity()),"Normal vision must leave colors alone");
        var rng=new Random(2026);
        foreach(var pair in models)
        {
            var screen=ColorMath.Build(new Look {Vision=pair.Key});
            int total=0,fixedPairs=0,others=0,broken=0;
            for(int tries=0;tries<600000&&(total<500||others<500);tries++)
            {
                var a=new[]{rng.NextDouble()*255,rng.NextDouble()*255,rng.NextDouble()*255};var b=new[]{rng.NextDouble()*255,rng.NextDouble()*255,rng.NextDouble()*255};
                if(Delta(a,b)<=25)continue;
                double before=Delta(SeenBy(a,pair.Value),SeenBy(b,pair.Value));
                double after=Delta(SeenBy(Shown(screen,a),pair.Value),SeenBy(Shown(screen,b),pair.Value));
                if(before<8&&total<500){total++;if(after>=15)fixedPairs++;}
                else if(before>=15&&others<500){others++;if(after<8)broken++;}
            }
            Check(total>=300&&fixedPairs*100.0/total>=60,pair.Key+" should make most colors that look alike to that viewer clearly different: "+fixedPairs+" of "+total);
            Check(broken*100.0/Math.Max(1,others)<4,pair.Key+" must not make colors that were easy to tell apart look alike: "+broken+" of "+others);
        }
        // The classic case: red and green for a green-blind viewer.
        var deutanScreen=ColorMath.Build(new Look {Vision="Deuteranopia"});
        double before2=Delta(SeenBy(new[]{200.0,40,40},models["Deuteranopia"]),SeenBy(new[]{40.0,150,40},models["Deuteranopia"]));
        double after2=Delta(SeenBy(Shown(deutanScreen,new[]{200.0,40,40}),models["Deuteranopia"]),SeenBy(Shown(deutanScreen,new[]{40.0,150,40}),models["Deuteranopia"]));
        Check(before2<8&&after2>=25,"Red and green should come apart for a green-blind viewer: "+before2+" -> "+after2);
        // The strength of the aid: none at 0, the full aid at 100 (what profiles saved before it get), and half way between at 50.
        var fullAid=ColorMath.Build(new Look {Vision="Deuteranopia",VisionStrength=100});
        var noAid=ColorMath.Build(new Look {Vision="Deuteranopia",VisionStrength=0});
        var halfAid=ColorMath.Build(new Look {Vision="Deuteranopia",VisionStrength=50});
        Check(ColorMath.Equal(noAid,ColorMath.Build(new Look())),"A color blindness aid at strength 0 should change nothing");
        Check(ColorMath.Equal(fullAid,deutanScreen)&&new Look().VisionStrength==100,"The full strength should be the aid as it always was, and the default");
        bool half=true;for(int i=0;i<25;i++)if(Math.Abs(halfAid[i]-(fullAid[i]+noAid[i])/2f)>.0005f)half=false;
        Check(half,"Half strength should be half way between no aid and the full aid");
        Console.WriteLine("PASS: color blindness modes measured against a simulation of each deficiency: most confusable colors are pulled apart and clear colors stay clear; the strength runs from none to the full aid.");
    }}
