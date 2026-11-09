using System;

namespace Lucent
{
    internal static class ZoomColorCompensation
    {
        // Columns in MAGCOLOREFFECT describe each output channel.
        public static float[] Build(float[] matrix)
        {
            var a=new double[3,3];
            var normal=new double[3,3];
            for(int row=0;row<3;row++)
            for(int col=0;col<3;col++)a[row,col]=matrix[col*5+row];
            for(int row=0;row<3;row++)
            for(int col=0;col<3;col++)
            {
                for(int k=0;k<3;k++)normal[row,col]+=a[k,row]*a[k,col];
                if(row==col)normal[row,col]+=.000001;
            }
            var augmented=new double[3,6];
            for(int row=0;row<3;row++)
            for(int col=0;col<3;col++){augmented[row,col]=normal[row,col];augmented[row,col+3]=row==col?1:0;}
            for(int pivot=0;pivot<3;pivot++)
            {
                int best=pivot;
                for(int row=pivot+1;row<3;row++)if(Math.Abs(augmented[row,pivot])>Math.Abs(augmented[best,pivot]))best=row;
                for(int col=0;col<6;col++)
                {
                    double temp=augmented[pivot,col];
                    augmented[pivot,col]=augmented[best,col];
                    augmented[best,col]=temp;
                }
                double divisor=augmented[pivot,pivot];
                for(int col=0;col<6;col++)augmented[pivot,col]/=divisor;
                for(int row=0;row<3;row++)
                {
                    if(row==pivot)continue;
                    double factor=augmented[row,pivot];
                    for(int col=0;col<6;col++)augmented[row,col]-=factor*augmented[pivot,col];
                }
            }
            var result=new float[24];
            double norm=0;
            for(int row=0;row<3;row++)
            {
                for(int col=0;col<3;col++)
                {
                    result[row*4+col]=(float)a[row,col];
                    norm+=a[row,col]*a[row,col];
                    double inverse=0;
                    for(int k=0;k<3;k++)inverse+=augmented[row,k+3]*a[col,k];
                    result[12+row*4+col]=(float)inverse;
                }
                result[row*4+3]=matrix[20+row]+matrix[15+row];
            }
            result[15]=(float)(1/Math.Max(.000001,norm));
            return result;
        }
    }
}
