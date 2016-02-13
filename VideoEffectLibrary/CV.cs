using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Media.Imaging;


namespace VideoEffectLibrary
{
	public class MarkerRecognizer
	{
		[ComImport]
		[Guid("5b0d3235-4dba-4d44-865e-8f1d0e4fd04d")]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		unsafe interface IMemoryBufferByteAccess
		{
			void GetBuffer(out byte* buffer, out uint capacity);
		}

		unsafe delegate void PixelAction(int bytes, byte* ptr, BitmapPlaneDescription desc);
		SoftwareBitmap _image;

		#region ctor
		public int KernelSize = 2;
		public double Treshold = 0.7;
		private BitArray _binary;
		private int _width;
		private int _height;

		public MarkerRecognizer(SoftwareBitmap image)
		{
			_image = image;
		}

		#endregion

		unsafe public void Recognize()
		{
			PreparePixels(GreyscaleProcess);
		}

		unsafe void PreparePixels(PixelAction action)
		{
			// Effect is hard-coded to operate on BGRA8 format only
			if (_image.BitmapPixelFormat == BitmapPixelFormat.Bgra8)
			{
				// In BGRA8 format, each pixel is defined by 4 bytes
				const int BYTES_PER_PIXEL = 4;

				using (var buffer = _image.LockBuffer(BitmapBufferAccessMode.ReadWrite))
				using (var reference = buffer.CreateReference())
				{
					if (reference is IMemoryBufferByteAccess)
					{
						// Get a pointer to the pixel buffer
						byte* data;
						uint capacity;
						((IMemoryBufferByteAccess)reference).GetBuffer(out data, out capacity);

						// Get information about the BitmapBuffer
						var desc = buffer.GetPlaneDescription(0);
						_width = desc.Width;
						_height = desc.Height;

						// Iterate over all pixels
						action(BYTES_PER_PIXEL, data, desc);
					}
				}
			}
		}

		private unsafe void GreyscaleProcess(int BYTES_PER_PIXEL, byte* data, BitmapPlaneDescription desc)
		{
			_binary = new BitArray(desc.Width * desc.Height);
			for (int row = 0; row < desc.Height; row++)
			{
				for (int col = 0; col < desc.Width; col++)
				{
					// Index of the current pixel in the buffer (defined by the next 4 bytes, BGRA8)
					var currPixel = desc.StartIndex + desc.Stride * row + BYTES_PER_PIXEL * col;

					// Read the current pixel information into b,g,r channels (leave out alpha channel)
					var b = data[currPixel + 0]; // Blue
					var g = data[currPixel + 1]; // Green
					var r = data[currPixel + 2]; // Red

					var average = (byte)(r * 0.299 + g * 0.587 + b * 0.114 + 0.5);
					if(average > Treshold * 255)
					{
						data[currPixel + 0] = 0;
						data[currPixel + 1] = 0;
						data[currPixel + 2] = 0;
						_binary[col * desc.Height + row] = true;
					}
					else
					{
						data[currPixel + 0] = 255;
						data[currPixel + 1] = 255;
						data[currPixel + 2] = 255;
						_binary[col * desc.Height + row] = false;
					}
				}
			}
			//
			//Sobel(BYTES_PER_PIXEL, data, desc);
		}

		private unsafe void Detect()
		{
			for(int y = 0; y < _height; y++)
			{
				for (int x = 0; x < _width; x++)
				{
					if(_binary[y * _height + x] == true)
					{

					}
				}
			}
		}

	

		private EdgePoint Search(int x, int y)
		{
			EdgePoint center;
			var horizontal = y;
		}


		public class EdgePoint
		{
			public int X { get; set; }
			public int Y { get; set; }
			public Vector2 Slope { get; set; }
			public int Theta { get; set; }
		}

		public class Line
		{
			public LinkedList<EdgePoint> Points = new LinkedList<EdgePoint>();
		}

		//private unsafe void Sobel(int BYTES_PER_PIXEL, byte* data, BitmapPlaneDescription desc)
		//{
		//	var GX = new int[3, 3];
		//	var GY = new int[3, 3];

		//	GX[0, 0] = -1; GX[0, 1] = 0; GX[0, 2] = 1;
		//	GX[1, 0] = -2; GX[1, 1] = 0; GX[1, 2] = 2;
		//	GX[2, 0] = -1; GX[2, 1] = 0; GX[2, 2] = 1;

		//	GY[0, 0] = -1; GY[0, 1] = -2; GY[0, 2] = -1;
		//	GY[1, 0] = 0; GY[1, 1] = 0; GY[1, 2] = 0;
		//	GY[2, 0] = 1; GY[2, 1] = 2; GY[2, 2] = 1;

		//	for (int y = 1; y < desc.Height - 1; y++)
		//	{
		//		for (int x = 1; x < desc.Width - 1; x++)
		//		{
		//			var px =	(GX[2, 2] * binary[x - 1, y - 1]) + (GX[2, 1] * binary[x, y - 1]) + (GX[2, 0] * binary[x + 1, y - 1]) +
		//						(GX[1, 2] * binary[x - 1, y])     + (GX[1, 1] * binary[x, y])     +	(GX[1, 0] * binary[x + 1, y]) +
		//						(GX[0, 2] * binary[x - 1, y + 1]) + (GX[0, 1] * binary[x, y + 1]) + (GX[0, 0] * binary[x + 1, y + 1]);

		//			var py =	(GY[2, 2] * binary[x - 1, y - 1]) + (GY[2, 1] * binary[x, y - 1]) + (GY[2, 0] * binary[x + 1, y - 1]) +
		//						(GY[1, 2] * binary[x - 1, y])     + (GY[1, 1] * binary[x, y])     +	(GY[1, 0] * binary[x + 1, y]) +
		//						(GY[0, 2] * binary[x - 1, y + 1]) + (GY[0, 1] * binary[x, y + 1]) + (GY[0, 0] * binary[x + 1, y + 1]);


		//			var val = Math.Sqrt((px * px) + (py * py));

		//			double theta = Math.Atan2(py, px) * 180 / Math.PI + 180;
		//			//var slope = Vector2.Normalize(new Vector2(px, py));
		//			//EdgePoint point = new EdgePoint { X = x, Y = y, Slope = slope, Theta = theta };
		//			//points.AddLast(point);

		//			var currPixel = desc.StartIndex + desc.Stride * y + BYTES_PER_PIXEL * x;

		//			if (val > Treshold * 255)
		//			{
		//				//alternative
		//				//(theta >= 0 && theta < 45) || (theta <= 360 && theta >= 315) || (theta >= 135 && theta < 225)
		//				if ((theta >= 45 && theta < 135) || (theta >= 225 && theta < 315))
		//				{
		//					data[currPixel + 0] = 0;
		//					data[currPixel + 1] = 0;
		//					data[currPixel + 2] = 255;
		//				}
		//				else
		//				{
		//					data[currPixel + 0] = 0;
		//					data[currPixel + 1] = 255;
		//					data[currPixel + 2] = 0;
		//				}
		//			}
		//			else
		//			{
		//				data[currPixel + 0] = 0;
		//				data[currPixel + 1] = 0;
		//				data[currPixel + 2] = 0;
		//			}
		//		}
		//	}
		//}

	}
}
