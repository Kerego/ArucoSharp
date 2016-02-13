using System.Collections.Generic;
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

		public void ProcessFrame(ProcessVideoFrameContext context)
		{
			if (context.InputFrame.SoftwareBitmap == null)
				return;

			var softwarebitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, context.InputFrame.SoftwareBitmap.PixelWidth, context.InputFrame.SoftwareBitmap.PixelHeight, context.OutputFrame.SoftwareBitmap.BitmapAlphaMode);
			context.InputFrame.SoftwareBitmap.CopyTo(softwarebitmap);

			MarkerRecognizer cv = new MarkerRecognizer(softwarebitmap);
			cv.Treshold = (double)_configuration["tolerance"];
			cv.Recognize();
			softwarebitmap.CopyTo(context.OutputFrame.SoftwareBitmap);
		}
		IPropertySet _configuration;

		public void SetEncodingProperties(VideoEncodingProperties encodingProperties, IDirect3DDevice device)
		{

		}

		public void SetProperties(IPropertySet configuration)
		{
			_configuration = configuration;
		}
	}
}