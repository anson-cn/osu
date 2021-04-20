// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Timing;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// Encapsulates gameplay timing logic and provides a <see cref="GameplayClock"/> via DI for gameplay components to use.
    /// </summary>
    public abstract class GameplayClockContainer : Container
    {
        /// <summary>
        /// The final clock which is exposed to gameplay components.
        /// </summary>
        public GameplayClock GameplayClock { get; private set; }

        /// <summary>
        /// Whether gameplay is paused.
        /// </summary>
        public readonly BindableBool IsPaused = new BindableBool();

        /// <summary>
        /// The adjustable source clock used for gameplay. Should be used for seeks and clock control.
        /// </summary>
        protected readonly DecoupleableInterpolatingFramedClock AdjustableSource;

        /// <summary>
        /// The offset at which to start playing. Affects the time which the clock is reset to via <see cref="Reset"/>.
        /// </summary>
        protected virtual double StartOffset => 0;

        /// <summary>
        /// The source clock.
        /// </summary>
        protected IClock SourceClock { get; private set; }

        /// <summary>
        /// Creates a new <see cref="GameplayClockContainer"/>.
        /// </summary>
        /// <param name="sourceClock">The source <see cref="IClock"/> used for timing.</param>
        protected GameplayClockContainer(IClock sourceClock)
        {
            SourceClock = sourceClock;

            RelativeSizeAxes = Axes.Both;

            AdjustableSource = new DecoupleableInterpolatingFramedClock { IsCoupled = false };
            IsPaused.BindValueChanged(OnIsPausedChanged);
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

            dependencies.CacheAs(GameplayClock = CreateGameplayClock(AdjustableSource));
            GameplayClock.IsPaused.BindTo(IsPaused);

            return dependencies;
        }

        /// <summary>
        /// Starts gameplay.
        /// </summary>
        public virtual void Start()
        {
            // Ensure that the source clock is set.
            ChangeSource(SourceClock);

            if (!AdjustableSource.IsRunning)
            {
                // Seeking the decoupled clock to its current time ensures that its source clock will be seeked to the same time
                // This accounts for the clock source potentially taking time to enter a completely stopped state
                Seek(GameplayClock.CurrentTime);

                AdjustableSource.Start();
            }

            IsPaused.Value = false;
        }

        /// <summary>
        /// Seek to a specific time in gameplay.
        /// </summary>
        /// <param name="time">The destination time to seek to.</param>
        public virtual void Seek(double time) => AdjustableSource.Seek(time);

        /// <summary>
        /// Stops gameplay.
        /// </summary>
        public virtual void Stop() => IsPaused.Value = true;

        /// <summary>
        /// Resets this <see cref="GameplayClockContainer"/> and the source to an initial state ready for gameplay.
        /// </summary>
        public virtual void Reset()
        {
            AdjustableSource.Seek(StartOffset);
            AdjustableSource.Stop();

            // Make sure the gameplay clock takes on the new time, otherwise the adjustable source will be seeked to the gameplay clock time in Start().
            GameplayClock.UnderlyingClock.ProcessFrame();

            if (!IsPaused.Value)
                Start();
        }

        /// <summary>
        /// Changes the source clock.
        /// </summary>
        /// <param name="sourceClock">The new source.</param>
        protected void ChangeSource(IClock sourceClock) => AdjustableSource.ChangeSource(SourceClock = sourceClock);

        protected override void Update()
        {
            if (!IsPaused.Value)
                GameplayClock.UnderlyingClock.ProcessFrame();

            base.Update();
        }

        /// <summary>
        /// Invoked when the value of <see cref="IsPaused"/> is changed to start or stop the <see cref="AdjustableSource"/> clock.
        /// </summary>
        /// <param name="isPaused">Whether the clock should now be paused.</param>
        protected virtual void OnIsPausedChanged(ValueChangedEvent<bool> isPaused)
        {
            if (isPaused.NewValue)
                AdjustableSource.Stop();
            else
                AdjustableSource.Start();
        }

        /// <summary>
        /// Creates the final <see cref="GameplayClock"/> which is exposed via DI to be used by gameplay components.
        /// </summary>
        /// <remarks>
        /// Any intermediate clocks such as platform offsets should be applied here.
        /// </remarks>
        /// <param name="source">The <see cref="IFrameBasedClock"/> providing the source time.</param>
        /// <returns>The final <see cref="GameplayClock"/>.</returns>
        protected abstract GameplayClock CreateGameplayClock(IFrameBasedClock source);
    }
}
