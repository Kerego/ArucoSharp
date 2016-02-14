using CrosswordSolver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VideoEffectLibrary;
using Windows.Foundation.Collections;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;

namespace VideoEffectComponent
{

	public sealed class MyVideoEffectDefinition : IVideoEffectDefinition
	{
		public string ActivatableClassId
		{
			get
			{
				return typeof(TresholdVideoEffect).FullName;
			}
		}

		public IPropertySet Properties
		{
			get
			{
				return null;
			}
		}
	}

	public sealed class TresholdVideoEffect : IBasicVideoEffect, IMediaExtension
	{
		public bool IsReadOnly
		{
			get
			{
				return false;
			}
		}

		public IReadOnlyList<VideoEncodingProperties> SupportedEncodingProperties
		{
			get
			{
				var properties = new List<VideoEncodingProperties>();
				properties.Add(VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Bgra8, 640, 480));
				return properties;
			}
		}

		public MediaMemoryTypes SupportedMemoryTypes
		{
			get
			{
				return MediaMemoryTypes.Cpu;
			}
		}

		public bool TimeIndependent
		{
			get
			{
				return false;
			}
		}

		public void Close(MediaEffectClosedReason reason)
		{

		}

		public void DiscardQueuedFrames()
		{

		}

		MarkerRecognizer recognizer;
		Solver solver;
		int prevCount = 0;
		int prevValue = 0;

		public void ProcessFrame(ProcessVideoFrameContext context)
		{
			if (context.InputFrame.SoftwareBitmap == null)
				return;


			var softwarebitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, context.InputFrame.SoftwareBitmap.PixelWidth, context.InputFrame.SoftwareBitmap.PixelHeight, context.OutputFrame.SoftwareBitmap.BitmapAlphaMode);
			context.InputFrame.SoftwareBitmap.CopyTo(softwarebitmap);
			
			recognizer.Treshold = (double)_configuration["tolerance"];
			recognizer.Hue = (int)_configuration["hue"];

			var result = recognizer.Recognize(softwarebitmap);
			var s = string.Empty;
			if (result != null)
			{
				for (int i = 0; i < result.GetLength(0); i++)
				{
					for (int j = 0; j < result.GetLength(1); j++)
					{
						var t = result[i, j];
						s += t == 0 ? " 0" : t.ToString();
						s += ' ';
					}
					s += Environment.NewLine;
				}
			}

			if (result != null)
			{
				prevCount = 0;
				var matrix = new int[10, 10]
			{
				{   0,  0, -1,  0,  0,  0,  0, -1,  0,  0 },
				{   0,  0, -1,  0,  0,  0,  0, -1,  0,  0 },
				{  -1, -1, -1,  0,  0, -1,  0, -1,  0,  0 },
				{   0,  0, -1,  0, -1, -1, -1, -1, -1, -1 },
				{   0,  0, -1,  0,  0, -1,  0, -1,  0,  0 },
				{   0,  0, -1,  0,  0, -1,  0, -1,  0,  0 },
				{   0,  0, -1,  0,  0, -1,  0,  0,  0,  0 },
				{  -1, -1, -1, -1, -1, -1,  0, -1,  0,  0 },
				{   0,  0, -1,  0,  0,  0,  0,  0,  0,  0 },
				{   0,  0, -1,  0,  0,  0,  0,  0,  0,  0 },
			};

				var test = solver.Test(matrix, 10, 10);
				char[,] solvedCrossword = null;
				if (test)
				{
					solvedCrossword = solver.FirstTest(10, 10);

					//var s = string.Empty;
					//for (int i = 0; i < solvedCrossword.GetLength(0); i++)
					//{
					//	for (int j = 0; j < solvedCrossword.GetLength(1); j++)
					//	{
					//		var t = solvedCrossword[i, j];
					//		s += (t == '\0') ? '*' : t;
					//	}
					//	s += Environment.NewLine;
					//}
				}
			}

			if (prevValue == recognizer.DetectedCenters.Count)
				prevCount++;
			_configuration["result"] = recognizer.DetectedCenters.Count;
			_configuration["centers"] = recognizer.DetectedCenters;
			prevValue = recognizer.DetectedCenters.Count;

			softwarebitmap.CopyTo(context.OutputFrame.SoftwareBitmap);
		}

		IPropertySet _configuration;
		IList<string> _dictionary;

		public void SetEncodingProperties(VideoEncodingProperties encodingProperties, IDirect3DDevice device)
		{

		}

		public void SetProperties(IPropertySet configuration)
		{
			_configuration = configuration;
			_dictionary = _configuration["dictionary"] as IList<string>;
			solver = new Solver(_dictionary);
			recognizer = new MarkerRecognizer();
		}
	}
}