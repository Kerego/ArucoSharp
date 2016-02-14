using System;
using System.Collections.Generic;
using System.Linq;

using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;

namespace VideoEffectLibrary
{

	public struct Point
	{
		public int X;
		public int Y;
	}

	public class MarkerRecognizer
	{
		[ComImport]
		[Guid("5b0d3235-4dba-4d44-865e-8f1d0e4fd04d")]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		unsafe interface IMemoryBufferByteAccess
		{
			void GetBuffer(out byte* buffer, out uint capacity);
		}

		public List<Point> DetectedCenters = new List<Point>();

		#region ctor
		public int KernelSize = 2;
		public double Treshold = 0.85;
		public int Hue = 330;
		private bool[,] _binary;
		private int _width;
		private int _height;
		private int _DistanceInRadiusesH = 4;
		private int _DistanceInRadiusesV = 3;

		#endregion

		public unsafe int[,] Recognize(SoftwareBitmap _image)
		{
			int[,] matrix = null;
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
						matrix = GreyscaleProcess(BYTES_PER_PIXEL, data, desc);
					}
				}
			}
			return matrix;
		}

		private unsafe int[,] GreyscaleProcess(int BYTES_PER_PIXEL, byte* data, BitmapPlaneDescription desc)
		{
			_binary = new bool[desc.Width, desc.Height];
			for (int row = 0; row < desc.Height; row++)
			{
				for (int col = 0; col < desc.Width; col++)
				{
					// Index of the current pixel in the buffer (defined by the next 4 bytes, BGRA8)
					var currPixel = desc.StartIndex + desc.Stride * row + BYTES_PER_PIXEL * col;

					// Read the current pixel information into b,g,r channels (leave out alpha channel)
					double b = data[currPixel + 0]; // Blue
					double g = data[currPixel + 1]; // Green
					double r = data[currPixel + 2]; // Red

					b = b / 255.0;
					g = g / 255.0;
					r = r / 255.0;
					double max = Math.Max(r, Math.Max(g, b));
					double min = Math.Min(r, Math.Min(g, b));

					double v, s, h;
					v = max;               // v
					var delta = max - min;
					if (max != 0)
						s = delta / max;
					else {
						// r = g = b = 0		// s = 0, v is undefined
						s = 0;
						h = -1;
					}
					if (r == max)
						h = (g - b) / delta;       // between yellow & magenta
					else if (g == max)
						h = 2 + (b - r) / delta;   // between cyan & yellow
					else
						h = 4 + (r - g) / delta;   // between magenta & cyan
					h *= 60;               // degrees
					if (h < 0)
						h += 360;

					if(h > Hue && v > Treshold)
					{
						data[currPixel + 0] = 0;
						data[currPixel + 1] = 0;
						data[currPixel + 2] = 0;
						_binary[col, row] = true;
					}
					else
					{
						data[currPixel + 0] = 255;
						data[currPixel + 1] = 255;
						data[currPixel + 2] = 255;
						_binary[col, row] = false;
					}

				}
			}
			return Detect(data, desc);
		}

		private unsafe int[,] Detect(byte* data, BitmapPlaneDescription desc)
		{
			DetectedCenters.Clear();
			LinkedList<Circle> circles = new LinkedList<Circle>();
			var tryFullImage = true;
			//if (DetectedCenters.Any())
			//{
			//	var temp = new List<Point>();
			//	foreach (var item in DetectedCenters)
			//	{
			//		if (_binary[item.X, item.Y] == false)
			//		{
			//			tryFullImage = true;
			//			break;
			//		}

			//		var result = SearchCircle(item.X, item.Y);
			//		if (result.HasValue)
			//		{
			//			circles.AddLast(result.Value);
			//			temp.Add(item);
			//		}
			//	}

			//	if(temp.Count == DetectedCenters.Count)
			//		DetectedCenters = temp;
			//	else
			//	{
			//		DetectedCenters.Clear();
			//		tryFullImage = true;
			//	}

			//}
			//else
			//	tryFullImage = true;

			if(tryFullImage)
			{
				for (int y = 0; y < _height; y++)
				{
					for (int x = 0; x < _width; x++)
					{
						if (_binary[x, y] == true)
						{
							var result = SearchSolid(x, y);
							if (result.HasValue)
							{
								circles.AddLast(result.Value);
								DetectedCenters.Add(new Point { X = result.Value.Position.X, Y = result.Value.Position.Y });
							}
						}
					}
				}

				foreach (var circle in circles)
				{
					for (int i = -5; i <= 5; i++)
						for (int j = -5; j <= 5; j++)
						{
							if (circle.Position.X + i < 5 || circle.Position.Y + j < 5 || circle.Position.X + i > _width - 5 || circle.Position.Y + j > _height - 5)
								continue;
							var currPixel = desc.StartIndex + desc.Stride * (circle.Position.Y + j) + 4 * (circle.Position.X + i);
							data[currPixel + 0] = 0;
							data[currPixel + 1] = 0;
							data[currPixel + 2] = 255;
						}
				}
			}
			

			if (circles.Count == 0)
				return null;
			
			double avgRadius = circles.Sum(x => x.Radius) / circles.Count;

			if (avgRadius == 0)
				return null;
			var first = circles.OrderBy(x => x.Position.X).First();

			//vertical
			double maxY = circles.Max(x => x.Position.Y);
			double minY = circles.Min(x => x.Position.Y);
			//horizontal
			double maxX = circles.Max(x => x.Position.X);
			double minX = circles.Min(x => x.Position.X);

			if (circles.Count > 1)
			{
				var minH = int.MaxValue;
				var minV = int.MaxValue;

				foreach (var circle in circles)
					foreach (var circle2 in circles)
					{
						if (!circle.Equals(circle2))
						{
							var x = Math.Abs((circle.Position.X - circle2.Position.X));
							if (x < minH && x > 25)
								minH = x;
							var y = Math.Abs((circle.Position.Y - circle2.Position.Y));
							if (y < minV && y > 25)
								minV = y ;
						}
					}
				//adaptive
				////_DistanceInRadiusesH = minH;
				////_DistanceInRadiusesV = minV;

				// old hardcoded
				var horizontalCount = (int)Math.Round(((maxX - minX) / avgRadius / _DistanceInRadiusesH + 1));
				var verticalCount = (int)Math.Round(((maxY - minY) / avgRadius / _DistanceInRadiusesV + 1));

				//adaptive
				//var horizontalCount = (int)Math.Round(((maxX - minX) / minH)) + 1;
				//var verticalCount = (int)Math.Round(((maxY - minY) / minV)) + 1;

				var matrix = new int[verticalCount, horizontalCount];

				foreach (var circle in circles)
				{
					var h = (int)Math.Round((circle.Position.X - minX) / avgRadius / _DistanceInRadiusesH);
					var v = (int)Math.Round((circle.Position.Y - minY) / avgRadius / _DistanceInRadiusesV);

					//var h = (int)Math.Round((circle.Position.X - minX) / minH );
					//var v = (int)Math.Round((circle.Position.Y - minY) / minV );

					if (h < horizontalCount && v < verticalCount)
						matrix[v, h] = -1;
				}
				return matrix;

			}
			return null;

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
	

		private Point CheckForCircle(int x, int y)
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


			currentX = x;
			currentY = y;
			//left diagonal
			while (currentX > 0 && currentY < _height &&  _binary[currentX--, currentY++] == true) ;

			leftX = x - currentX;
			leftY = currentY - y;
			
			return new Point
			{
				Y = verLength,
				X = (int)(Math.Max(leftX, rightX) * 1.41)
			};
		}

		private Circle? SearchSolid(int x, int y)
		{
			
			var parameters = CheckForCircle(x, y);
			
			var queue = new Queue<Point>();
			queue.Enqueue(new Point { X = x, Y = y });
			int minY = int.MaxValue;
			int minX = int.MaxValue;
			int maxX = 0;
			int maxY = 0;

			var pixelCount = 0;
			Point pt;


			while(queue.Any())
			{
				pixelCount++;
				var point = queue.Dequeue();

				if (point.X > maxX)
					maxX = point.X;
				if (point.X < minX)
					minX = point.X;
				if (point.Y > maxY)
					maxY = point.Y;
				if (point.Y < minY)
					minY = point.Y;

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

		

			var radius = ((maxY - minY) / 2 + (maxX - minX) / 2) / 2;

			//if (maxX - minX == 0)
			//	return null;

			if (pixelCount < 100 && radius < 25)
				return null;


			//if (parameters.Y < radius || parameters.X < radius)
			//	return null;

			var proportion = ((double)(maxY - minY) / (maxX - minX));

			if (proportion > 3 || proportion < 0.2)
				return null;

			return new Circle
			{
				Position = new Point
				{
					X = (maxX + minX) / 2,
					Y = (maxY + minY) / 2
				},
				Radius = radius
			};
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
