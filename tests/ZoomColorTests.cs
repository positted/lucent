using System;
using System.Reflection;
using Lucent;

class ZoomColorTests
{
    static float[] Transform(float[] m,float[] c)
    {
        var result=new float[3];
        for(int j=0;j<3;j++){double value=m[20+j];for(int i=0;i<3;i++)value+=m[i*5+j]*c[i];result[j]=(float)Math.Max(0,Math.Min(1,value));}
        return result;
    }
    static float[] Correct(float[] data,float[] desired)
    {
        var value=new float[3];float gray=Math.Max(desired[0],Math.Max(desired[1],desired[2]));
        var residual=new float[3];
        for(int j=0;j<3;j++){float mapped=data[j*4+3];for(int k=0;k<3;k++)mapped+=data[j*4+k]*gray;residual[j]=desired[j]-mapped;}
        for(int j=0;j<3;j++){float v=gray;for(int k=0;k<3;k++)v+=data[12+j*4+k]*residual[k];value[j]=Math.Max(0,Math.Min(1,v));}
        for(int iteration=0;iteration<12;iteration++)
        {
            var error=new float[3];
            for(int j=0;j<3;j++){float mapped=data[j*4+3];for(int k=0;k<3;k++)mapped+=data[j*4+k]*value[k];error[j]=Math.Max(0,Math.Min(1,mapped))-desired[j];}
            for(int j=0;j<3;j++){float gradient=0;for(int k=0;k<3;k++)gradient+=data[k*4+j]*error[k];value[j]=Math.Max(0,Math.Min(1,value[j]-data[15]*gradient));}
        }
        return value;
    }
    static void Main()
    {
        var assembly=typeof(Look).Assembly;
        var build=assembly.GetType("Lucent.ColorMath").GetMethod("Build");
        var compensate=assembly.GetType("Lucent.ZoomColorCompensation").GetMethod("Build");
        var random=new Random(73);
        foreach(var look in new[]{
            new Look(),new Look {Saturation=300},new Look {Dim=85},
            new Look {DarkEnabled=true,DarkStrength=100,BlackLevel=55,DarkColor="Green"},
            new Look {Red=100},new Look {Saturation=250,Red=55,Dim=35,DarkEnabled=true,DarkStrength=60,DarkColor="Cyan"}
        })
        {
            var m=(float[])build.Invoke(null,new object[]{look});
            var correction=(float[])compensate.Invoke(null,new object[]{m});
            double worst=0;
            for(int n=0;n<1000;n++)
            {
                var pixel=new[]{(float)random.NextDouble(),(float)random.NextDouble(),(float)random.NextDouble()};
                var desired=Transform(m,pixel);var output=Transform(m,Correct(correction,desired));
                for(int j=0;j<3;j++){if(float.IsNaN(output[j]))throw new Exception("Nonfinite correction");worst=Math.Max(worst,Math.Abs(desired[j]-output[j]));}
            }
            Console.WriteLine("Maximum color difference "+worst.ToString("0.00000"));
            if(worst>.025)throw new Exception("Zoom color correction exceeded tolerance");
        }
        Console.WriteLine("PASS: 6000 color samples including clipped dark boost, 300% vibrance, dim and red-only.");
    }
}
