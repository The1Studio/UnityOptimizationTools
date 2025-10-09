namespace TheOne.UITemplate.Editor.Optimization.Services
{
    using System;
    using UnityEditor;

    /// <summary>
    /// Shared progress tracking service for optimization operations.
    /// Consolidates progress bar logic used across all optimization tools.
    /// REUSES Unity's EditorUtility.DisplayProgressBar() - no duplication.
    /// </summary>
    public class ProgressTracker
    {
        private string currentOperation = "";
        private int totalSteps = 0;
        private int currentStep = 0;
        private DateTime startTime;
        private bool isActive = false;

        /// <summary>
        /// Start tracking progress for an operation.
        /// </summary>
        /// <param name="operationName">Name of the operation (e.g., "Analyzing Textures")</param>
        /// <param name="total">Total number of steps</param>
        public void Start(string operationName, int total)
        {
            this.currentOperation = operationName;
            this.totalSteps = total;
            this.currentStep = 0;
            this.startTime = DateTime.Now;
            this.isActive = true;

            EditorUtility.DisplayProgressBar(this.currentOperation, "Starting...", 0f);
        }

        /// <summary>
        /// Increment the progress by 1 step.
        /// </summary>
        /// <param name="info">Optional info message to display</param>
        public void Increment(string info = null)
        {
            if (!this.isActive) return;

            this.currentStep++;
            this.UpdateProgressBar(info);
        }

        /// <summary>
        /// Set progress to a specific step.
        /// </summary>
        /// <param name="step">The current step number</param>
        /// <param name="info">Optional info message to display</param>
        public void SetProgress(int step, string info = null)
        {
            if (!this.isActive) return;

            this.currentStep = step;
            this.UpdateProgressBar(info);
        }

        /// <summary>
        /// Update progress with a custom percentage (0.0 to 1.0).
        /// Useful when total steps are unknown.
        /// </summary>
        /// <param name="percentage">Progress percentage (0.0 to 1.0)</param>
        /// <param name="info">Info message to display</param>
        public void SetProgressPercentage(float percentage, string info)
        {
            if (!this.isActive) return;

            EditorUtility.DisplayProgressBar(this.currentOperation, info, percentage);
        }

        /// <summary>
        /// Complete the current operation and clear the progress bar.
        /// </summary>
        public void Complete()
        {
            if (!this.isActive) return;

            EditorUtility.ClearProgressBar();
            this.isActive = false;

            var duration = DateTime.Now - this.startTime;
            UnityEngine.Debug.Log($"{this.currentOperation} completed in {duration.TotalSeconds:F2}s ({this.currentStep}/{this.totalSteps} steps)");
        }

        /// <summary>
        /// Cancel the current operation and clear the progress bar.
        /// </summary>
        public void Cancel()
        {
            if (!this.isActive) return;

            EditorUtility.ClearProgressBar();
            this.isActive = false;

            UnityEngine.Debug.LogWarning($"{this.currentOperation} cancelled at step {this.currentStep}/{this.totalSteps}");
        }

        /// <summary>
        /// Check if the user has requested cancellation via ESC key.
        /// Call this periodically in long-running loops.
        /// </summary>
        /// <returns>True if the user wants to cancel</returns>
        public bool IsCancellationRequested()
        {
            return EditorUtility.DisplayCancelableProgressBar(
                this.currentOperation,
                this.GetProgressInfo(),
                this.GetProgress()
            );
        }

        /// <summary>
        /// Get the current progress percentage (0.0 to 1.0).
        /// </summary>
        /// <returns>Progress percentage</returns>
        public float GetProgress()
        {
            if (this.totalSteps == 0) return 0f;
            return this.currentStep / (float)this.totalSteps;
        }

        /// <summary>
        /// Get estimated time remaining based on current progress.
        /// </summary>
        /// <returns>Estimated TimeSpan remaining, or null if cannot be calculated</returns>
        public TimeSpan? GetEstimatedTimeRemaining()
        {
            if (this.currentStep == 0 || this.totalSteps == 0) return null;

            var elapsed = DateTime.Now - this.startTime;
            var avgTimePerStep = elapsed.TotalSeconds / this.currentStep;
            var remainingSteps = this.totalSteps - this.currentStep;
            var estimatedSeconds = avgTimePerStep * remainingSteps;

            return TimeSpan.FromSeconds(estimatedSeconds);
        }

        /// <summary>
        /// Get a formatted progress info string.
        /// </summary>
        /// <param name="customInfo">Optional custom info to prepend</param>
        /// <returns>Formatted progress string</returns>
        private string GetProgressInfo(string customInfo = null)
        {
            var info = $"Processing {this.currentStep}/{this.totalSteps}";

            var eta = this.GetEstimatedTimeRemaining();
            if (eta.HasValue && eta.Value.TotalSeconds > 1)
                info += $" (ETA: {eta.Value.TotalSeconds:F0}s)";

            if (!string.IsNullOrEmpty(customInfo))
                info = $"{customInfo} - {info}";

            return info;
        }

        /// <summary>
        /// Update the Unity progress bar with current state.
        /// </summary>
        /// <param name="info">Optional custom info message</param>
        private void UpdateProgressBar(string info = null)
        {
            var progressInfo = this.GetProgressInfo(info);
            EditorUtility.DisplayProgressBar(this.currentOperation, progressInfo, this.GetProgress());
        }

        /// <summary>
        /// Get statistics about the current operation.
        /// </summary>
        /// <returns>Tuple of (current step, total steps, progress percentage, elapsed time)</returns>
        public (int current, int total, float progress, TimeSpan elapsed) GetStats()
        {
            return (
                this.currentStep,
                this.totalSteps,
                this.GetProgress(),
                DateTime.Now - this.startTime
            );
        }
    }
}
