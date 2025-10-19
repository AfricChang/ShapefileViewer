// Encoding: UTF-8
using System;
using System.IO;
using DotSpatial.Data;

namespace ShapefileViewer.Utils
{
    public static class ShapefileLoader
    {
        public static IFeatureSet? Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var fs = Shapefile.OpenFile(path);
                return fs;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}