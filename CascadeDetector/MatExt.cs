﻿namespace CascadeDetector
{
    using OpenCvSharp;

    public static class MatExt
    {
        public static Mat OverLay(this Mat mat)
        {
            return new Mat(mat.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 0));
        }
    }
}