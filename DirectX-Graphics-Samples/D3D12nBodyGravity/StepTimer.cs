internal class StepTimer
{
	// Integer format represents time using 10,000,000 ticks per second.
	private const ulong TicksPerSecond = 10000000;

	private ulong m_elapsedTicks;
	private uint m_frameCount;
	private uint m_framesPerSecond;
	private uint m_framesThisSecond;
	private ulong m_leftOverTicks;
	private readonly long m_qpcFrequency;
	private long m_qpcLastTime;
	private readonly ulong m_qpcMaxDelta;
	private ulong m_qpcSecondCounter;
	private ulong m_targetElapsedTicks;
	private ulong m_totalTicks;

	public StepTimer()
	{
		QueryPerformanceFrequency(out m_qpcFrequency);
		QueryPerformanceCounter(out m_qpcLastTime);

		// Initialize max delta to 1/10 of a second.
		m_qpcMaxDelta = (ulong)m_qpcFrequency / 10;
	}

	public double ElapsedSeconds => TicksToSeconds(m_elapsedTicks);

	// Get elapsed time since the previous Update call.
	public ulong ElapsedTicks => m_elapsedTicks;

	// Get total number of updates since start of the program.
	public uint FrameCount => m_frameCount;

	// Get the current framerate.
	public uint FramesPerSecond => m_framesPerSecond;

	// Set whether to use fixed or variable timestep mode.
	public bool IsFixedTimeStep { get; set; }

	public double TotalSeconds => TicksToSeconds(m_totalTicks);

	// Get total time since the start of the program.
	public ulong TotalTicks => m_totalTicks;

	// After an intentional timing discontinuity (for instance a blocking IO operation) call this to avoid having the fixed timestep logic
	// attempt a set of catch-up Update calls.
	public void ResetElapsedTime()
	{
		QueryPerformanceCounter(out m_qpcLastTime);

		m_leftOverTicks = 0;
		m_framesPerSecond = 0;
		m_framesThisSecond = 0;
		m_qpcSecondCounter = 0;
	}

	public void SetTargetElapsedSeconds(double targetElapsed) => m_targetElapsedTicks = SecondsToTicks(targetElapsed);

	// Set how often to call Update when in fixed timestep mode.
	public void SetTargetElapsedTicks(ulong targetElapsed) => m_targetElapsedTicks = targetElapsed;

	// Update timer state, calling the specified Update function the appropriate number of times.
	public void Tick(Action? update = default)
	{
		// Query the current time.

		QueryPerformanceCounter(out long currentTime);

		ulong timeDelta = (ulong)(currentTime - m_qpcLastTime);

		m_qpcLastTime = currentTime;
		m_qpcSecondCounter += timeDelta;

		// Clamp excessively large time deltas (e.g. after paused in the debugger).
		if (timeDelta > m_qpcMaxDelta)
		{
			timeDelta = m_qpcMaxDelta;
		}

		// Convert QPC units into a canonical tick format. This cannot overflow due to the previous clamp.
		timeDelta *= TicksPerSecond;
		timeDelta /= (ulong)m_qpcFrequency;

		uint lastFrameCount = m_frameCount;

		if (IsFixedTimeStep)
		{
			// Fixed timestep update logic

			// If the app is running very close to the target elapsed time (within 1/4 of a millisecond) just clamp the clock to exactly
			// match the target value. This prevents tiny and irrelevant errors from accumulating over time. Without this clamping, a game
			// that requested a 60 fps fixed update, running with vsync enabled on a 59.94 NTSC display, would eventually accumulate enough
			// tiny errors that it would drop a frame. It is better to just round small deviations down to zero to leave things running smoothly.

			if (Math.Abs((int)(timeDelta - m_targetElapsedTicks)) < (int)(TicksPerSecond / 4000))
			{
				timeDelta = m_targetElapsedTicks;
			}

			m_leftOverTicks += timeDelta;

			while (m_leftOverTicks >= m_targetElapsedTicks)
			{
				m_elapsedTicks = m_targetElapsedTicks;
				m_totalTicks += m_targetElapsedTicks;
				m_leftOverTicks -= m_targetElapsedTicks;
				m_frameCount++;

				update?.Invoke();
			}
		}
		else
		{
			// Variable timestep update logic.
			m_elapsedTicks = timeDelta;
			m_totalTicks += timeDelta;
			m_leftOverTicks = 0;
			m_frameCount++;

			update?.Invoke();
		}

		// Track the current framerate.
		if (m_frameCount != lastFrameCount)
		{
			m_framesThisSecond++;
		}

		if (m_qpcSecondCounter >= (ulong)m_qpcFrequency)
		{
			m_framesPerSecond = m_framesThisSecond;
			m_framesThisSecond = 0;
			m_qpcSecondCounter %= (ulong)m_qpcFrequency;
		}
	}

	private static ulong SecondsToTicks(double seconds) => (ulong)(seconds * TicksPerSecond);

	private static double TicksToSeconds(ulong ticks) => (double)ticks / TicksPerSecond;
}