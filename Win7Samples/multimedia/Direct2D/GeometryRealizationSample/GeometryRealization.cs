using System.ComponentModel.DataAnnotations;
using Vanara.PInvoke;
using static Vanara.PInvoke.D2d1;
using static Vanara.PInvoke.DXGI;

[Flags]
public enum REALIZATION_CREATION_OPTIONS
{
	// Generate mesh
	REALIZATION_CREATION_OPTIONS_ALIASED = 1,

	// Generate opacity mask
	REALIZATION_CREATION_OPTIONS_ANTI_ALIASED = 2,

	// Retain pointer to original geometry for unrealized rendering
	REALIZATION_CREATION_OPTIONS_UNREALIZED = 4,

	// Generate fill realization
	REALIZATION_CREATION_OPTIONS_FILLED = 8,

	// Generate stroke realization
	REALIZATION_CREATION_OPTIONS_STROKED = 16
}

public enum REALIZATION_RENDER_MODE
{
	// Force the use of the realization
	REALIZATION_RENDER_MODE_FORCE_REALIZED = 0,

	// Force the use of the original geometry
	REALIZATION_RENDER_MODE_FORCE_UNREALIZED = 1,

	// Key off of the render-target to decide:
	// SW: Unrealized
	// HW: Realized
	REALIZATION_RENDER_MODE_DEFAULT = 2
}

//+-----------------------------------------------------------------------------
//
// Interface:
// IGeometryRealization
//
// Description:
// Encapsulates various mesh and/or opacity mask instances to provide
// efficient rendering of complex primitives.
//
//------------------------------------------------------------------------------
[Guid("a0b504a9-be04-44a7-ae05-71ac89c1b6a7")]
internal interface IGeometryRealization
{
	// Render the stroke realization to the render target
	void Draw(ID2D1RenderTarget pRT, ID2D1Brush pBrush, REALIZATION_RENDER_MODE mode);

	// Render the fill realization to the render target
	void Fill(ID2D1RenderTarget pRT, ID2D1Brush pBrush, REALIZATION_RENDER_MODE mode);

	// Discard the current realization's contents and replace with new contents.
	//
	// Note: This method will attempt to reuse the existing bitmaps (but will replace the bitmaps if they aren't large enough). Since the
	// cost of destroying a texture can be surprisingly astronomical, using this method can be substantially more performant than recreating
	// a new realization every time.
	//
	// Note: pWorldTransform is the transform that the realization will be optimized for. If, at the time of rendering, the render target's
	// transform is the same as pWorldTransform, the realization will appear identical to the unrealized version. Otherwise, quality will be degraded.
	void Update(ID2D1Geometry pGeometry, REALIZATION_CREATION_OPTIONS options, in D2D_MATRIX_3X2_F? pWorldTransform,
		float strokeWidth, ID2D1StrokeStyle? pIStrokeStyle);
}

//+-----------------------------------------------------------------------------
//
// Interface:
// IGeometryRealizationFactory
//
//------------------------------------------------------------------------------
[Guid("27866d9f-8865-461d-8a10-2531156398b2")]
internal interface IGeometryRealizationFactory
{
	// Create a geometry realization.
	//
	// Note: Here, pWorldTransform is the transform that the realization will be optimized for. If, at the time of rendering, the render
	// target's transform is the same as the pWorldTransform passed in here then the realization will look identical to the unrealized
	// version. Otherwise, quality will be degraded.
	IGeometryRealization CreateGeometryRealization(ID2D1Geometry? pGeometry = null,
		REALIZATION_CREATION_OPTIONS options = REALIZATION_CREATION_OPTIONS.REALIZATION_CREATION_OPTIONS_ALIASED,
		in D2D_MATRIX_3X2_F? pWorldTransform = null, float strokeWidth = 0f, ID2D1StrokeStyle? pIStrokeStyle = null);
}

//+-----------------------------------------------------------------------------
//
//  Class:
//      GeometryRealization
//
//------------------------------------------------------------------------------
internal class GeometryRealization : IGeometryRealization
{
	// The maximum granularity of bitmap sizes we allow for AA realizations.
	private const uint sc_bitmapChunkSize = 64;

	private D2D_RECT_F m_fillMaskDestBounds;
	private D2D_RECT_F m_fillMaskSourceBounds;
	private uint m_maxRealizationDimension;

	private ID2D1Mesh? m_pFillMesh;
	private ID2D1BitmapRenderTarget? m_pFillRT;
	private ID2D1Geometry? m_pGeometry;
	private ID2D1RenderTarget m_pRT;
	private ID2D1Mesh? m_pStrokeMesh;
	private ID2D1BitmapRenderTarget? m_pStrokeRT;
	private ID2D1StrokeStyle? m_pStrokeStyle;

	private D2D_MATRIX_3X2_F m_realizationTransform;
	private D2D_MATRIX_3X2_F m_realizationTransformInv;

	private bool m_realizationTransformIsIdentity;

	private D2D_RECT_F m_strokeMaskDestBounds;
	private D2D_RECT_F m_strokeMaskSourceBounds;

	private float m_strokeWidth;
	private bool m_swRT;

	public GeometryRealization(ID2D1RenderTarget pRT, uint maxRealizationDimension, ID2D1Geometry? pGeometry,
		REALIZATION_CREATION_OPTIONS options, in D2D_MATRIX_3X2_F? pWorldTransform, float strokeWidth,
		ID2D1StrokeStyle? pIStrokeStyle)
	{
		m_pRT = pRT;
		m_swRT = pRT.IsSupported(RenderTargetProperties(D2D1_RENDER_TARGET_TYPE.D2D1_RENDER_TARGET_TYPE_SOFTWARE));
		m_maxRealizationDimension = maxRealizationDimension;

		Update(pGeometry, options, pWorldTransform, strokeWidth, pIStrokeStyle);
	}

	//+-----------------------------------------------------------------------------
	//
	// Method:
	// GeometryRealization::Draw
	//
	//------------------------------------------------------------------------------
	public void Draw(ID2D1RenderTarget pRT, ID2D1Brush pBrush, REALIZATION_RENDER_MODE mode) =>
		RenderToTarget(false, // => stroke
			pRT,
			pBrush,
			mode);

	//+-----------------------------------------------------------------------------
	//
	// Method:
	// GeometryRealization::Fill
	//
	//------------------------------------------------------------------------------
	public void Fill(ID2D1RenderTarget pRT, ID2D1Brush pBrush, REALIZATION_RENDER_MODE mode) =>
		RenderToTarget(true, // => fill
			pRT,
			pBrush,
			mode);

	//+-----------------------------------------------------------------------------
	//
	// Method:
	// GeometryRealization::Update
	//
	// Description:
	// Discard the current realization's contents and replace with new
	// contents.
	//
	// Note: This method attempts to reuse the existing bitmaps (but will
	// replace the bitmaps if they aren't large enough). Since the cost of
	// destroying a texture can be surprisingly astronomical, using this
	// method can be substantially more performant than recreating a new
	// realization every time.
	//
	// Note: Here, pWorldTransform is the transform that the realization will
	// be optimized for. If, at the time of rendering, the render target's
	// transform is the same as the pWorldTransform passed in here then the
	// realization will look identical to the unrealized version. Otherwise,
	// quality will be degraded.
	//
	//------------------------------------------------------------------------------
	public void Update(ID2D1Geometry? pGeometry, REALIZATION_CREATION_OPTIONS options, in D2D_MATRIX_3X2_F? pWorldTransform, float strokeWidth, ID2D1StrokeStyle? pIStrokeStyle)
	{
		if (pWorldTransform.HasValue)
		{
			m_realizationTransform = pWorldTransform.Value;
			m_realizationTransformIsIdentity = m_realizationTransform.IsIdentity;
		}
		else
		{
			m_realizationTransform = D2D_MATRIX_3X2_F.Identity();
			m_realizationTransformIsIdentity = true;
		}

		// We're about to create our realizations with the world transform applied to them. When we go to actually render the realization,
		// though, we'll want to "undo" this realization and instead apply the render target's current transform.
		//
		// Note: we keep track to see if the passed in realization transform is the identity. This is a small optimization that saves us from
		// having to multiply matrices when we go to draw the realization.

		m_realizationTransformInv = m_realizationTransform;
		m_realizationTransformInv.Invert();

		if ((options & REALIZATION_CREATION_OPTIONS.REALIZATION_CREATION_OPTIONS_UNREALIZED) != 0 || m_swRT)
		{
			m_pGeometry = pGeometry;
			m_pStrokeStyle = pIStrokeStyle;
			m_strokeWidth = strokeWidth;
		}

		if ((options & REALIZATION_CREATION_OPTIONS.REALIZATION_CREATION_OPTIONS_ANTI_ALIASED) != 0)
		{
			// Antialiased realizations are implemented using opacity masks.

			if ((options & REALIZATION_CREATION_OPTIONS.REALIZATION_CREATION_OPTIONS_FILLED) != 0)
			{
				GenerateOpacityMask(true, // => filled
					m_pRT,
					m_maxRealizationDimension,
					ref m_pFillRT,
					pGeometry!,
					pWorldTransform,
					strokeWidth,
					pIStrokeStyle,
					out m_fillMaskDestBounds,
					out m_fillMaskSourceBounds);
			}

			if ((options & REALIZATION_CREATION_OPTIONS.REALIZATION_CREATION_OPTIONS_STROKED) != 0)
			{
				GenerateOpacityMask(false, // => stroked
					m_pRT,
					m_maxRealizationDimension,
					ref m_pStrokeRT,
					pGeometry!,
					pWorldTransform,
					strokeWidth,
					pIStrokeStyle,
					out m_strokeMaskDestBounds,
					out m_strokeMaskSourceBounds);
			}
		}

		if ((options & REALIZATION_CREATION_OPTIONS.REALIZATION_CREATION_OPTIONS_ALIASED) != 0)
		{
			// Aliased realizations are implemented using meshes.

			if ((options & REALIZATION_CREATION_OPTIONS.REALIZATION_CREATION_OPTIONS_FILLED) != 0)
			{
				ID2D1Mesh pMesh = m_pRT.CreateMesh();

				ID2D1TessellationSink pSink = pMesh.Open();

				pGeometry!.Tessellate(pWorldTransform.GetValueOrDefault(), default, pSink);
				pSink.Close();
				m_pFillMesh = pMesh;
			}

			if ((options & REALIZATION_CREATION_OPTIONS.REALIZATION_CREATION_OPTIONS_STROKED) != 0)
			{
				// In order generate the mesh corresponding to the stroke of a geometry, we first "widen" the geometry and then tessellate
				// the result.
				m_pRT.GetFactory(out var pFactory);

				ID2D1PathGeometry pPathGeometry = pFactory.CreatePathGeometry();

				ID2D1GeometrySink pGeometrySink = pPathGeometry.Open();

				pGeometry!.Widen(strokeWidth,
					pIStrokeStyle,
					pWorldTransform.GetValueOrDefault(),
					0f,
					pGeometrySink);

				pGeometrySink.Close();

				ID2D1Mesh pMesh = m_pRT.CreateMesh();

				ID2D1TessellationSink pSink = pMesh.Open();

				pPathGeometry.Tessellate(default, // world transform (already handled in Widen)
					default,
					pSink);

				pSink.Close();
				m_pStrokeMesh = pMesh;
			}
		}
	}

	//+-----------------------------------------------------------------------------
	//
	// Method:
	// GeometryRealization::GenerateOpacityMask
	//
	// Notes:
	// This method is the trickiest part of doing realizations. Conceptually,
	// we're creating a grayscale bitmap that represents the geometry. We'll
	// reuse an existing bitmap if we can, but if not, we'll create the
	// smallest possible bitmap that contains the geometry. In either, case,
	// though, we'll keep track of the portion of the bitmap we actually used
	// (the source bounds), so when we go to draw the realization, we don't
	// end up drawing a bunch of superfluous transparent pixels.
	//
	// We also have to keep track of the "dest" bounds, as more than likely
	// the bitmap has to be translated by some amount before being drawn.
	//
	//------------------------------------------------------------------------------
	private void GenerateOpacityMask(bool fill, ID2D1RenderTarget pBaseRT, uint maxRealizationDimension, ref ID2D1BitmapRenderTarget? ppBitmapRT,
		ID2D1Geometry pIGeometry, in D2D_MATRIX_3X2_F? pWorldTransform, float strokeWidth, ID2D1StrokeStyle? pStrokeStyle,
		out D2D_RECT_F pMaskDestBounds, out D2D_RECT_F pMaskSourceBounds)
	{
		float scaleX = 1.0f;
		float scaleY = 1.0f;

		ID2D1BitmapRenderTarget? pCompatRT = ppBitmapRT;

		ID2D1SolidColorBrush pBrush = pBaseRT.CreateSolidColorBrush(new(1.0f, 1.0f, 1.0f, 1.0f));

		pBaseRT.GetDpi(out var dpiX, out var dpiY);

		D2D_RECT_F bounds = fill ? pIGeometry.GetBounds(pWorldTransform) : pIGeometry.GetWidenedBounds(strokeWidth, pStrokeStyle, pWorldTransform);

		// A rect where left > right is defined to be empty.
		//
		// The slightly baroque expression used below is an idiom that also correctly handles NaNs (i.e., if any of the coordinates of the
		// bounds is a NaN, we want to treat the bounds as empty)
		D2D_RECT_F inflatedPixelBounds = new();
		if (!(bounds.left <= bounds.right) || !(bounds.top <= bounds.bottom))
		{
			// Bounds are empty or ill-defined.

			// Make up a fake bounds
			inflatedPixelBounds.top = 0.0f;
			inflatedPixelBounds.left = 0.0f;
			inflatedPixelBounds.bottom = 1.0f;
			inflatedPixelBounds.right = 1.0f;
		}
		else
		{
			// We inflate the pixel bounds by 1 in each direction to ensure we have a border of completely transparent pixels around the
			// geometry. This ensures that when the realization is stretched the alpha ramp still smoothly falls off to 0 rather than being
			// clipped by the rect.
			inflatedPixelBounds.top = (float)Math.Floor(bounds.top * dpiY / 96) - 1.0f;
			inflatedPixelBounds.left = (float)Math.Floor(bounds.left * dpiX / 96) - 1.0f;
			inflatedPixelBounds.bottom = (float)Math.Ceiling(bounds.bottom * dpiY / 96) + 1.0f;
			inflatedPixelBounds.right = (float)Math.Ceiling(bounds.right * dpiX / 96) + 1.0f;
		}

		// Compute the width and height of the underlying bitmap we will need.
		// Note: We round up the width and height to be a multiple of sc_bitmapChunkSize. We do this primarily to ensure that we aren't
		// constantly reallocating bitmaps in the case where a realization is being zoomed in on slowly and updated frequently.

		var inflatedIntegerPixelSize = SizeU((uint)(inflatedPixelBounds.right - inflatedPixelBounds.left),
			(uint)(inflatedPixelBounds.bottom - inflatedPixelBounds.top));

		// Round up
		inflatedIntegerPixelSize.width =
			(inflatedIntegerPixelSize.width + sc_bitmapChunkSize - 1) / sc_bitmapChunkSize * sc_bitmapChunkSize;

		// Round up
		inflatedIntegerPixelSize.height =
		(inflatedIntegerPixelSize.height + sc_bitmapChunkSize - 1) / sc_bitmapChunkSize * sc_bitmapChunkSize;

		// Compute the bounds we will pass to FillOpacityMask (which are in Device Independent Pixels).
		//
		// Note: The DIP bounds ref do ref not use the rounded coordinates, since this would cause us to render superfluous,
		// fully-transparent pixels, which would hurt fill rate.
		D2D_RECT_F inflatedDipBounds = RectF(inflatedPixelBounds.left * 96 / dpiX,
		inflatedPixelBounds.right * 96 / dpiX,
		inflatedPixelBounds.top * 96 / dpiY,
		inflatedPixelBounds.bottom * 96 / dpiY);

		D2D_SIZE_U currentRTSize;
		if (pCompatRT is not null)
		{
			currentRTSize = pCompatRT.GetPixelSize();
		}
		else
		{
			// This will force the creation of a new target
			currentRTSize = SizeU(0, 0);
		}

		// We need to ensure that our desired render target size isn't larger than the Math.Max allowable bitmap size. If it is, we need to
		// scale the bitmap down by the appropriate amount.

		if (inflatedIntegerPixelSize.width > maxRealizationDimension)
		{
			scaleX = maxRealizationDimension / (float)(inflatedIntegerPixelSize.width);
			inflatedIntegerPixelSize.width = maxRealizationDimension;
		}

		if (inflatedIntegerPixelSize.height > maxRealizationDimension)
		{
			scaleY = maxRealizationDimension / (float)(inflatedIntegerPixelSize.height);
			inflatedIntegerPixelSize.height = maxRealizationDimension;
		}

		// If the necessary pixel dimensions are less than half the existing bitmap's dimensions (in either direction), force the bitmap to
		// be reallocated to save memory.
		//
		// Note: The fact that we use > rather than >= is important for a subtle
		// reason: We'd like to have the property that repeated small changes in geometry size do not cause repeated reallocations of memory.
		// >= does not ensure this property in the case where the geometry size is close to sc_bitmapChunkSize, but > does.
		//
		// Example:
		//
		// Assume sc_bitmapChunkSize is 64 and the initial geometry width is 63 pixels. This will get rounded up to 64, and we will allocate
		// a bitmap with width 64. Now, say, we zoom in slightly, so the new geometry width becomes 65 pixels. This will get rounded up to
		// 128 pixels, and a new bitmap will be allocated. Now, say the geometry drops back down to 63 pixels. This will get rounded up to
		// 64. If we used >=, this would cause another reallocation. Since we use >, on the other hand, the 128 pixel bitmap will be reused.

		if (currentRTSize.width > 2 * inflatedIntegerPixelSize.width || currentRTSize.height > 2 * inflatedIntegerPixelSize.height)
		{
			pCompatRT = null;
			currentRTSize.width = currentRTSize.height = 0;
		}

		if (inflatedIntegerPixelSize.width > currentRTSize.width || inflatedIntegerPixelSize.height > currentRTSize.height)
		{
			pCompatRT = null;
		}

		if (pCompatRT is null)
		{
			// Make sure our new rendertarget is strictly larger than before.
			currentRTSize.width = Math.Max(inflatedIntegerPixelSize.width, currentRTSize.width);

			currentRTSize.height = Math.Max(inflatedIntegerPixelSize.height, currentRTSize.height);

			D2D1_PIXEL_FORMAT alphaOnlyFormat = PixelFormat(DXGI_FORMAT.DXGI_FORMAT_A8_UNORM, D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED);

			pCompatRT = pBaseRT.CreateCompatibleRenderTarget(default, // desiredSize
				currentRTSize,
				alphaOnlyFormat,
				D2D1_COMPATIBLE_RENDER_TARGET_OPTIONS.D2D1_COMPATIBLE_RENDER_TARGET_OPTIONS_NONE);
		}

		// Translate the geometry so it is flush against the left and top sides of the render target.

		D2D_MATRIX_3X2_F translateMatrix =
		D2D_MATRIX_3X2_F.Translation(-inflatedDipBounds.left, -inflatedDipBounds.top) *
			D2D_MATRIX_3X2_F.Scale(scaleX, scaleY);

		if (pWorldTransform is not null)
		{
			pCompatRT.SetTransform(pWorldTransform.Value * translateMatrix);
		}
		else
		{
			pCompatRT.SetTransform(translateMatrix);
		}

		// Render the geometry.

		pCompatRT.BeginDraw();

		pCompatRT.Clear(default);

		if (fill)
		{
			pCompatRT.FillGeometry(pIGeometry,
			pBrush);
		}
		else
		{
			pCompatRT.DrawGeometry(pIGeometry,
			pBrush,
			strokeWidth,
			pStrokeStyle);
		}

		pCompatRT.EndDraw();

		// Report back the source and dest bounds (to be used as input parameters to FillOpacityMask.
		pMaskDestBounds = inflatedDipBounds;

		pMaskSourceBounds = new(0.0f, 0.0f,
			(float)(inflatedDipBounds.right - inflatedDipBounds.left) * scaleX,
			(float)(inflatedDipBounds.bottom - inflatedDipBounds.top) * scaleY);

		if (ppBitmapRT != pCompatRT)
		{
			ppBitmapRT = pCompatRT;
		}
	}

	//+-----------------------------------------------------------------------------
	//
	// Method:
	// GeometryRealization::RenderToTarget
	//
	//------------------------------------------------------------------------------
	private void RenderToTarget(bool fill,
	ID2D1RenderTarget pRT,
	ID2D1Brush pBrush,
	REALIZATION_RENDER_MODE mode
	)
	{
		D2D1_ANTIALIAS_MODE originalAAMode = pRT.GetAntialiasMode();
		D2D_MATRIX_3X2_F originalTransform = default;

		if (((mode == REALIZATION_RENDER_MODE.REALIZATION_RENDER_MODE_DEFAULT) && m_swRT) ||
		(mode == REALIZATION_RENDER_MODE.REALIZATION_RENDER_MODE_FORCE_UNREALIZED)
		)
		{
			if (m_pGeometry is null)
			{
				// We're being asked to render the geometry unrealized, but we weren't created with REALIZATION_CREATION_OPTIONS_UNREALIZED.
				throw new Exception("Not created unrealized.");
			}

			if (fill)
			{
				pRT.FillGeometry(m_pGeometry,
				pBrush);
			}
			else
			{
				pRT.DrawGeometry(m_pGeometry,
				pBrush,
				m_strokeWidth,
				m_pStrokeStyle);
			}
		}
		else
		{
			if (originalAAMode != D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED)
			{
				pRT.SetAntialiasMode(D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
			}

			if (!m_realizationTransformIsIdentity)
			{
				pRT.GetTransform(out originalTransform);
				pRT.SetTransform(m_realizationTransformInv * originalTransform);
			}

			if (originalAAMode == D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_PER_PRIMITIVE)
			{
				if (fill)
				{
					if (m_pFillRT is not null)
					{
						ID2D1Bitmap pBitmap = m_pFillRT.GetBitmap();

						// Note: The antialias mode must be set to aliased prior to calling FillOpacityMask.
						pRT.FillOpacityMask(pBitmap,
						pBrush,
						D2D1_OPACITY_MASK_CONTENT.D2D1_OPACITY_MASK_CONTENT_GRAPHICS,
						m_fillMaskDestBounds,
						m_fillMaskSourceBounds);
					}
				}
				else
				{
					if (m_pStrokeRT is not null)
					{
						ID2D1Bitmap pBitmap = m_pStrokeRT.GetBitmap();

						// Note: The antialias mode must be set to aliased prior to calling FillOpacityMask.
						pRT.FillOpacityMask(pBitmap,
						pBrush,
						D2D1_OPACITY_MASK_CONTENT.D2D1_OPACITY_MASK_CONTENT_GRAPHICS,
						m_strokeMaskDestBounds,
						m_strokeMaskSourceBounds);
					}
				}
			}
			else
			{
				if (fill)
				{
					if (m_pFillMesh is not null)
					{
						pRT.FillMesh(m_pFillMesh,
						pBrush);
					}
				}
				else
				{
					if (m_pStrokeMesh is not null)
					{
						pRT.FillMesh(m_pStrokeMesh,
						pBrush);
					}
				}
			}

			pRT.SetAntialiasMode(originalAAMode);

			if (!m_realizationTransformIsIdentity)
			{
				pRT.SetTransform(originalTransform);
			}
		}
	}
}

//+-----------------------------------------------------------------------------
//
// Class:
// GeometryRealizationFactory
//
//------------------------------------------------------------------------------
internal class GeometryRealizationFactory(ID2D1RenderTarget pRT, [Range(1, uint.MaxValue)] uint maxRealizationDimension = 0xffffffff) : IGeometryRealizationFactory
{
	private readonly uint m_maxRealizationDimension = Math.Min(pRT.GetMaximumBitmapSize(), maxRealizationDimension);
	private readonly ID2D1RenderTarget m_pRT = pRT;

	//+-----------------------------------------------------------------------------
	//
	// Method:
	// GeometryRealizationFactory::CreateGeometryRealization
	//
	//------------------------------------------------------------------------------
	public IGeometryRealization CreateGeometryRealization(ID2D1Geometry? pGeometry = null,
		REALIZATION_CREATION_OPTIONS options = REALIZATION_CREATION_OPTIONS.REALIZATION_CREATION_OPTIONS_ALIASED,
		in D2D_MATRIX_3X2_F? pWorldTransform = null, float strokeWidth = 0f, ID2D1StrokeStyle? pIStrokeStyle = null) =>
		new GeometryRealization(m_pRT, m_maxRealizationDimension, pGeometry, options, pWorldTransform, strokeWidth, pIStrokeStyle);
}