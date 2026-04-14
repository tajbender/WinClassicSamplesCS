using static Vanara.PInvoke.DirectXMath;

internal class SimpleCamera
{
	private XMFLOAT3 m_initialPosition;
	private KeysPressed m_keysPressed;
	private XMFLOAT3 m_lookDirection = new(0, 0, -1);
	private float m_moveSpeed = 20;
	private float m_pitch;
	private XMFLOAT3 m_position;
	private float m_turnSpeed = XM_PIDIV2;
	private XMFLOAT3 m_upDirection = new(0, 1, 0);
	private float m_yaw = XM_PI;

	public SimpleCamera(XMFLOAT3 position)
	{
		m_initialPosition = position;
		Reset();
	}

	public XMMATRIX GetProjectionMatrix(float fov, float aspectRatio, float nearPlane, float farPlane) => XMMatrixPerspectiveFovRH(fov, aspectRatio, nearPlane, farPlane);

	public XMMATRIX GetViewMatrix() => XMMatrixLookToRH(XMLoadFloat3(m_position), XMLoadFloat3(m_lookDirection), XMLoadFloat3(m_upDirection));

	public void OnKeyDown(VK key)
	{
		switch (key)
		{
			case VK.VK_W:
				m_keysPressed.w = true;
				break;

			case VK.VK_A:
				m_keysPressed.a = true;
				break;

			case VK.VK_S:
				m_keysPressed.s = true;
				break;

			case VK.VK_D:
				m_keysPressed.d = true;
				break;

			case VK.VK_LEFT:
				m_keysPressed.left = true;
				break;

			case VK.VK_RIGHT:
				m_keysPressed.right = true;
				break;

			case VK.VK_UP:
				m_keysPressed.up = true;
				break;

			case VK.VK_DOWN:
				m_keysPressed.down = true;
				break;

			case VK.VK_ESCAPE:
				Reset();
				break;
		}
	}

	public void OnKeyUp(VK key)
	{
		switch (key)
		{
			case VK.VK_W:
				m_keysPressed.w = false;
				break;

			case VK.VK_A:
				m_keysPressed.a = false;
				break;

			case VK.VK_S:
				m_keysPressed.s = false;
				break;

			case VK.VK_D:
				m_keysPressed.d = false;
				break;

			case VK.VK_LEFT:
				m_keysPressed.left = false;
				break;

			case VK.VK_RIGHT:
				m_keysPressed.right = false;
				break;

			case VK.VK_UP:
				m_keysPressed.up = false;
				break;

			case VK.VK_DOWN:
				m_keysPressed.down = false;
				break;
		}
	}

	public void SetMoveSpeed(float unitsPerSecond) => m_moveSpeed = unitsPerSecond;

	public void SetTurnSpeed(float radiansPerSecond) => m_turnSpeed = radiansPerSecond;

	public void Update(float elapsedSeconds)
	{
		// Calculate the move vector in camera space.
		XMFLOAT3 move = new(0, 0, 0);

		if (m_keysPressed.a)
			move.x -= 1.0f;
		if (m_keysPressed.d)
			move.x += 1.0f;
		if (m_keysPressed.w)
			move.z -= 1.0f;
		if (m_keysPressed.s)
			move.z += 1.0f;

		if (Math.Abs(move.x) > 0.1f && Math.Abs(move.z) > 0.1f)
		{
			XMVECTOR vector = XMLoadFloat3(move).XMVector3Normalize();
			move.x = vector.XMVectorGetX();
			move.z = vector.XMVectorGetZ();
		}

		float moveInterval = m_moveSpeed * elapsedSeconds;
		float rotateInterval = m_turnSpeed * elapsedSeconds;

		if (m_keysPressed.left)
			m_yaw += rotateInterval;
		if (m_keysPressed.right)
			m_yaw -= rotateInterval;
		if (m_keysPressed.up)
			m_pitch += rotateInterval;
		if (m_keysPressed.down)
			m_pitch -= rotateInterval;

		// Prevent looking too far up or down.
		m_pitch = Math.Min(m_pitch, XM_PIDIV4);
		m_pitch = Math.Max(-XM_PIDIV4, m_pitch);

		// Move the camera in model space.
		float x = (float)(move.x * -Math.Cos(m_yaw) - move.z * Math.Sin(m_yaw));
		float z = (float)(move.x * Math.Sin(m_yaw) - move.z * Math.Cos(m_yaw));
		m_position.x += x * moveInterval;
		m_position.z += z * moveInterval;

		// Determine the look direction.
		float r = (float)Math.Cos(m_pitch);
		m_lookDirection.x = r * (float)Math.Sin(m_yaw);
		m_lookDirection.y = (float)Math.Sin(m_pitch);
		m_lookDirection.z = r * (float)Math.Cos(m_yaw);
	}

	private void Reset()
	{
		m_position = m_initialPosition;
		m_yaw = XM_PI;
		m_pitch = 0;
		m_lookDirection = new(0, 0, -1);
	}

	private struct KeysPressed
	{
		public bool a;
		public bool d;
		public bool down;
		public bool left;
		public bool right;
		public bool s;
		public bool up;
		public bool w;
	}
}