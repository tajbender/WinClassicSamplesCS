/******************************************************************
*                                                                 *
*  RingBuffer                                                     *
*                                                                 *
******************************************************************/

using System.Diagnostics;

internal class RingBuffer<T>(int maxElements)
{
	private readonly T[] m_elements = new T[maxElements];
	private uint m_start = 0;

	public void Add(T element)
	{
		m_elements[(m_start + Count) % maxElements] = element;

		if (Count < maxElements)
		{
			Count++;
		}
		else
		{
			m_start = (m_start + 1) % (uint)maxElements;
		}
	}

	public uint Count { get; set; } = 0;

	public T First
	{
		get
		{
			Debug.Assert(Count > 0);
			return m_elements[m_start];
		}
	}

	public T Last
	{
		get
		{
			Debug.Assert(Count > 0);
			return m_elements[(m_start + Count - 1) % maxElements];
		}
	}

	public void Reset()
	{
		m_start = 0;
		Count = 0;
	}
}