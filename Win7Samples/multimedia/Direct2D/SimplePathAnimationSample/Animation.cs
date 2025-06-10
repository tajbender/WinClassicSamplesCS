/******************************************************************
*                                                                 *
* Base Animation Class                                            *
*                                                                 *
******************************************************************/
using System.Numerics;

abstract class Animation<T>(T start, T end, T duration) where T : INumber<T>, IComparable<T>, IConvertible
{
	protected T m_Duration = duration;

	public T Start { get; set; } = start;
	public T End { get; set; } = end;
	public T Duration { get => m_Duration; set => m_Duration = Max(T.Zero, value); }

	public T GetValue(T time)
	{
		time = Min(Max(time, T.Zero), m_Duration);
		return ComputeValue(time);
	}

	protected abstract T ComputeValue(T time);

	protected static T PowT(T value, T exponent) => (T)Convert.ChangeType(Math.Pow(value.ToDouble(null), exponent.ToDouble(null)), typeof(T));
	protected static T Max(T a, T b) => a.CompareTo(b) > 0 ? a : b;
	protected static T Min(T a, T b) => a.CompareTo(b) < 0 ? a : b;
	protected static readonly T Ten = (T)Convert.ChangeType(10.0, typeof(T));
	protected static readonly T Two = (T)Convert.ChangeType(2.0, typeof(T));
}

/******************************************************************
*                                                                 *
* Linearly Interpolate Between Start and End                      *
*                                                                 *
******************************************************************/
class LinearAnimation<T>(T start, T end, T duration) : Animation<T>(start, end, duration) where T : INumber<T>, IComparable<T>, IConvertible
{
	protected override T ComputeValue(T time) => Start + (End - Start) * (time / m_Duration);
}

/******************************************************************
*                                                                 *
*                                                                 *
*                                                                 *
******************************************************************/
class EaseInExponentialAnimation<T>(T start, T end, T duration) : Animation<T>(start, end, duration) where T : INumber<T>, IComparable<T>, IConvertible
{
	protected override T ComputeValue(T time) => Start + (End - Start) * PowT(Two, Ten * (time / m_Duration - T.One));
}

/******************************************************************
*                                                                 *
*                                                                 *
*                                                                 *
******************************************************************/
class EaseOutExponentialAnimation<T>(T start, T end, T duration) : Animation<T>(start, end, duration) where T : INumber<T>, IComparable<T>, IConvertible
{
	protected override T ComputeValue(T time) => Start + (End - Start) * (-PowT(Two, -Ten * time / m_Duration) + T.One);
}

/******************************************************************
*                                                                 *
*                                                                 *
*                                                                 *
******************************************************************/
class EaseInOutExponentialAnimation<T>(T start, T end, T duration) : Animation<T>(start, end, duration) where T : INumber<T>, IComparable<T>, IConvertible
{
	protected override T ComputeValue(T time)
	{
		//compute the current time relative to the midpoint
		time /= m_Duration / Two;
		//if we haven't reached the midpoint, we want to do the ease-in portion
		if (time < T.One)
		{
			return Start + (End - Start) / Two * PowT(Two, Ten * (time - T.One));
		}
		//otherwise, do the ease-out portion
		return Start + (End - Start) / Two * (-PowT(Two, -Ten * --time) + Two);
	}
}