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
		private bool[,] _binary;
		private int _width;
		private int _height;
		private int _DistanceInRadiusesH = 6;
		private int _DistanceInRadiusesV = 6;

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
			var stopwatch = Stopwatch.StartNew();
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

			stopwatch.Stop();
		}

		private unsafe void GreyscaleProcess(int BYTES_PER_PIXEL, byte* data, BitmapPlaneDescription desc)
		{
			_binary = new bool[desc.Width, desc.Height];
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


					//double factor = 259.0 * (200.0 + 255.0) / (255.0 * (259.0 - 200.0));
					//b = (byte)((factor * b - 128) + 128);
					//g = (byte)((factor * g - 128) + 128);
					//r = (byte)((factor * r - 128) + 128);

					var average = (byte)(r * 0.299 + g * 0.587 + b * 0.114);

					if (average > 0.5 * 255)
					{
						data[currPixel + 0] = 0;
						data[currPixel + 1] = 0;
						data[currPixel + 2] = 0;
						_binary[col, row] = false;
					}
					else
					{
						data[currPixel + 0] = 255;
						data[currPixel + 1] = 255;
						data[currPixel + 2] = 255;
						_binary[col, row] = true;
					}

					//data[currPixel + 0] = average;
					//data[currPixel + 1] = average;
					//data[currPixel + 2] = average;

				}
			}

			Detect(data, desc);
			//Sobel(BYTES_PER_PIXEL, data, desc);
		}

		private unsafe void Detect(byte* data, BitmapPlaneDescription desc)
		{
			LinkedList<Circle> circles = new LinkedList<Circle>();
			
			for (int y = 0; y < _height; y++)
			{
				for (int x = 0; x < _width; x++)
				{
					if(_binary[x, y] == true)
					{
						var sw = Stopwatch.StartNew();
						var result = Search(x, y);
						sw.Stop();
						if (result.HasValue)
							circles.AddLast(result.Value);
					}
				}
			}

			foreach(var circle in  circles)
			{
				for (int i = -5; i <= 5; i++)
					for (int j = -5; j <= 5; j++)
					{
						if (circle.Position.X + i < 5 || circle.Position.Y + j < 5 || circle.Position.X + i > _width -5 || circle.Position.Y + j > _height - 5)
							continue;
						var currPixel = desc.StartIndex + desc.Stride * (circle.Position.Y + j) + 4 * (circle.Position.X + i);
						data[currPixel + 0] = 0;
						data[currPixel + 1] = 0;
						data[currPixel + 2] = 255;
					}
			}
			if (circles.Count == 0)
				return;

			double avgRadius = circles.Sum(x => x.Radius) / circles.Count;
			var first = circles.OrderBy(x => x.Position.X).First();

			//vertical
			double maxY = circles.Max(x => x.Position.Y);
			double minY = circles.Min(x => x.Position.Y);
			//horizontal
			double maxX = circles.Max(x => x.Position.X);
			double minX = circles.Min(x => x.Position.X);



			var horizontalCount = (int)Math.Round(((maxX - minX) / avgRadius / _DistanceInRadiusesH + 1));
			var verticalCount = (int)Math.Round(((maxY - minY) / avgRadius / _DistanceInRadiusesV + 1));

			var matrix = new int[verticalCount, horizontalCount];
			
			foreach(var circle in circles)
			{
				var h = (int)Math.Round((circle.Position.X - minX) / avgRadius / _DistanceInRadiusesH);
				var v = (int)Math.Round((circle.Position.Y - minY) / avgRadius / _DistanceInRadiusesV);
				matrix[v, h] = -1;
			}

			//output

			//var s = string.Empty;
			//for (int i = 0; i < matrix.GetLength(0); i++)
			//{
			//	for (int j = 0; j < matrix.GetLength(1); j++)
			//	{
			//		s += matrix[i, j];
			//	}
			//	s += Environment.NewLine;
			//}
			//Debug.WriteLine(s);
		}

		private unsafe void DrawLineX(int x, int y, int count, byte* data, BitmapPlaneDescription desc)
		{
			for(int i = 0; i < count; i++)
			{
				var currPixel = desc.StartIndex + desc.Stride * y + 4 * (x + i);
				data[currPixel + 0] = 0;
				data[currPixel + 1] = 0;
				data[currPixel + 2] = 255;
			}
		}

		private unsafe void DrawLineY(int x, int y, int count, byte* data, BitmapPlaneDescription desc)
		{
			for (int i = 0; i < count; i++)
			{
				var currPixel = desc.StartIndex + desc.Stride * (y + i) + 4 * x;
				data[currPixel + 0] = 0;
				data[currPixel + 1] = 0;
				data[currPixel + 2] = 255;
			}
		}

		private int Distance(Point a, Point b)
		{
			return (int)Math.Sqrt(Math.Pow((b.X - a.X), 2) + Math.Pow((b.Y - a.Y), 2));
		}

		struct Circle
		{
			public Point Position;
			public int Radius;
		}

		struct Point
		{
			public int X;
			public int Y;
		}
	

		private Circle? Search(int x, int y)
		{

			var currentX = x;
			var currentY = y;
			var verLength = 0;
			var rightX = 0;
			var rightY = 0;
			var leftX = 0;
			var leftY = 0;

			//vertical
			while (currentY < _height && _binary[x, currentY++] == true) ;
			verLength = currentY - y;
			
			currentY = y;

			//right diagonal
			while (currentX < _width && currentY < _height && _binary[currentX++, currentY++] == true) ;

			rightX = currentX - x;
			rightY = currentY - y;

			if (Math.Abs(verLength - rightX - rightY) >= verLength * Treshold)
			{
				ClearObject(x, y);
				return null; // remove object
			}

			currentX = x;
			currentY = y;
			//left diagonal
			while (currentX > 0 && currentY < _height &&  _binary[currentX--, currentY++] == true) ;

			leftX = x - currentX;
			leftY = currentY - y;

			if (Math.Abs(verLength - leftX - leftY) >= verLength * Treshold)
			{
				ClearObject(x, y);
				return null; // remove object
			}

			//remove
			ClearObject(x, y);

			if (verLength < 20)
				return null;

			return new Circle	{
									Position = new Point
									{
										X = x,
										Y = y + verLength / 2
									},
									Radius = verLength / 2
								};
		}

		private void ClearObject(int x, int y)
		{

			var queue = new Queue<Point>();

			queue.Enqueue(new Point { X = x, Y = y });

			Point pt;
			while(queue.Any())
			{
				var point = queue.Dequeue();
				_binary[point.X, point.Y] = false;
				//neighbourhood
				for(int i = -1; i <= 1; i++)
					for(int j = -1; j <= 1; j++)
					{
						if (i == 0 && j == 0 || point.X + i < 0 || point.Y + j < 0 || point.X + i >= _width || point.Y + j >= _height)
							continue;
						if (_binary[point.X + i, point.Y + j] == true)
						{
							pt = new Point { X = point.X + i, Y = point.Y + j };
							_binary[pt.X, pt.Y] = false;
							queue.Enqueue(pt);
						}
					}
			}
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
