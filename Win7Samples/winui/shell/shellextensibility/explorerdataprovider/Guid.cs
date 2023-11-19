﻿using static Vanara.PInvoke.Ole32;

namespace explorerdataprovider;

public static class Guids
{
	// Level {F5A38A1F-3093-4dd9-9E6C-8762E4224F10}
	public static readonly Guid CAT_GUID_LEVEL = new(0xf5a38a1f, 0x3093, 0x4dd9, 0x9e, 0x6c, 0x87, 0x62, 0xe4, 0x22, 0x4f, 0x10);

	// Name {77559890-8E11-48e2-9B72-585FBA9CFFF4}
	public static readonly Guid CAT_GUID_NAME = new(0x77559890, 0x8e11, 0x48e2, 0x9b, 0x72, 0x58, 0x5f, 0xba, 0x9c, 0xff, 0xf4);

	// Type {D1873FB3-76BA-474d-BE69-ED52476DD7E3}
	public static readonly Guid CAT_GUID_SIDES = new(0xd1873fb3, 0x76ba, 0x474d, 0xbe, 0x69, 0xed, 0x52, 0x47, 0x6d, 0xd7, 0xe3);

	// Size {0210C647-A9B8-41fb-ACB7-3C57D27C5BC1}
	public static readonly Guid CAT_GUID_SIZE = new(0x210c647, 0xa9b8, 0x41fb, 0xac, 0xb7, 0x3c, 0x57, 0xd2, 0x7c, 0x5b, 0xc1);

	// The next category guid is NOT based on a column {5DE8798F-9E55-4a78-804E-274A906BC3B3}
	public static readonly Guid CAT_GUID_VALUE = new(0x5de8798f, 0x9e55, 0x4a78, 0x80, 0x4e, 0x27, 0x4a, 0x90, 0x6b, 0xc3, 0xb3);

	public static readonly Guid CLSID_FolderViewImpl = new(0xba16ce0e, 0x728c, 0x4fc9, 0x98, 0xe5, 0xd0, 0xb3, 0x5b, 0x38, 0x45, 0x97);

	public static readonly Guid CLSID_FolderViewImplContextMenu = new(0xbb8f539d, 0x3b97, 0x4473, 0x9e, 0x07, 0xc8, 0x24, 0x8c, 0x53, 0x24, 0x8e);

	// {CA133333-0D7D-47ff-BEE5-2C99DA47A7D9}
	public static readonly Guid GUID_Display = new(0xca133333, 0xd7d, 0x47ff, 0xbe, 0xe5, 0x2c, 0x99, 0xda, 0x47, 0xa7, 0xd9);

	// {3F6FA710-63A8-4843-92CE-F3216D2B20D6}
	public static readonly Guid GUID_Setting1 = new(0x3f6fa710, 0x63a8, 0x4843, 0x92, 0xce, 0xf3, 0x21, 0x6d, 0x2b, 0x20, 0xd6);

	// {43077C60-029C-4e1e-9CF6-9C38B9512342}
	public static readonly Guid GUID_Setting2 = new(0x43077c60, 0x29c, 0x4e1e, 0x9c, 0xf6, 0x9c, 0x38, 0xb9, 0x51, 0x23, 0x42);

	// {B8F3F98F-DDB6-48b3-BF93-81D2A791BFF9}
	public static readonly Guid GUID_Setting3 = new(0xb8f3f98f, 0xddb6, 0x48b3, 0xbf, 0x93, 0x81, 0xd2, 0xa7, 0x91, 0xbf, 0xf9);

	// {BE901C03-EA3B-4d3d-B6F7-C3D2FE94BE69}
	public static readonly Guid GUID_Settings = new(0xbe901c03, 0xea3b, 0x4d3d, 0xb6, 0xf7, 0xc3, 0xd2, 0xfe, 0x94, 0xbe, 0x69);

	// Col 2 name="Microsoft.SDKSample.AreaSize" {CE8B09DD-B2D6-4751-A207-4FFDD1A0F65C}
	public static readonly PROPERTYKEY PKEY_Microsoft_SDKSample_AreaSize = new(new(0xce8b09dd, 0xb2d6, 0x4751, 0xa2, 0x7, 0x4f, 0xfd, 0xd1, 0xa0, 0xf6, 0x5c), 3);

	// Col 4 name="Microsoft.SDKSample.DirectoryLevel" {581CF603-2925-4acf-BB5A-3D3EB39EACD3}
	public static readonly PROPERTYKEY PKEY_Microsoft_SDKSample_DirectoryLevel = new(new(0x581cf603, 0x2925, 0x4acf, 0xbb, 0x5a, 0x3d, 0x3e, 0xb3, 0x9e, 0xac, 0xd3), 3);

	// Col 3 name="Microsoft.SDKSample.NumberOfSides" {DADD3288-0380-4dd3-A5C4-CD7AE2A099F0}
	public static readonly PROPERTYKEY PKEY_Microsoft_SDKSample_NumberOfSides = new(new(0xdadd3288, 0x380, 0x4dd3, 0xa5, 0xc4, 0xcd, 0x7a, 0xe2, 0xa0, 0x99, 0xf0), 3);
}