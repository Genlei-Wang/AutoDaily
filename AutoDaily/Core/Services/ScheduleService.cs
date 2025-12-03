using System;
using System.Linq;
using System.Timers;
using AutoDaily.Core.Models;

namespace AutoDaily.Core.Services
{
    public class ScheduleService
    {
        private Timer _timer;
        private TaskService _taskService;
        private Action<Task> _onTaskTriggered;
        private DateTime? _lastTriggerTime = null; // 防止重复触发

        public ScheduleService(TaskService taskService, Action<Task> onTaskTriggered)
        {
            _taskService = taskService;
            _onTaskTriggered = onTaskTriggered;

            // 智能调度：根据距离目标时间的远近动态调整检查间隔
            // 距离目标时间 > 5分钟：每60秒检查一次
            // 距离目标时间 1-5分钟：每10秒检查一次
            // 距离目标时间 < 1分钟：每1秒检查一次（精确触发）
            _timer = new Timer(60000); // 初始60秒检查
            _timer.Elapsed += Timer_Elapsed;
            _timer.AutoReset = true;
            _timer.Start();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            CheckSchedule();
            AdjustTimerInterval(); // 动态调整检查间隔
        }

        private void AdjustTimerInterval()
        {
            var task = _taskService.GetCurrentTask();
            if (!task.Schedule.Enabled)
            {
                _timer.Interval = 60000; // 未启用时，60秒检查一次
                return;
            }

            var now = DateTime.Now;
            var scheduleTime = new DateTime(now.Year, now.Month, now.Day, 
                task.Schedule.Hour, task.Schedule.Minute, 0);
            
            // 如果今天已经过了，计算明天的
            if (now > scheduleTime)
            {
                scheduleTime = scheduleTime.AddDays(1);
            }

            var timeUntilSchedule = (scheduleTime - now).TotalMinutes;

            // 动态调整检查间隔
            if (timeUntilSchedule > 5)
            {
                _timer.Interval = 60000; // > 5分钟：每60秒检查
            }
            else if (timeUntilSchedule > 1)
            {
                _timer.Interval = 10000; // 1-5分钟：每10秒检查
            }
            else
            {
                _timer.Interval = 1000; // < 1分钟：每1秒检查（精确触发）
            }
        }

        private void CheckSchedule()
        {
            var task = _taskService.GetCurrentTask();
            if (!task.Schedule.Enabled)
            {
                _lastTriggerTime = null;
                return;
            }

            var now = DateTime.Now;
            var scheduleTime = new DateTime(now.Year, now.Month, now.Day, 
                task.Schedule.Hour, task.Schedule.Minute, 0);

            // 精确时间检查：当前时间 >= 设定时间，且在同一分钟内
            // 允许最多30秒的误差（考虑到检查间隔和系统延迟）
            // 如果超过30秒，就不再运行（用户要求）
            bool isTimeReached = now >= scheduleTime && 
                (now - scheduleTime).TotalSeconds <= 30;
            
            // 检查今天是否已经运行过
            bool notRunToday = !task.LastRun.HasValue || task.LastRun.Value.Date != now.Date;
            
            // 防止重复触发：如果上次触发时间与当前时间在同一分钟内，不触发
            bool notRecentlyTriggered = !_lastTriggerTime.HasValue || 
                (now - _lastTriggerTime.Value).TotalMinutes >= 1;

            if (isTimeReached && notRunToday && notRecentlyTriggered)
            {
                _lastTriggerTime = now;
                // 更新最后运行时间，防止重复触发
                task.LastRun = now;
                _taskService.UpdateCurrentTask(task);
                
                _onTaskTriggered?.Invoke(task);
            }
        }

        public DateTime? GetNextRunTime()
        {
            var task = _taskService.GetCurrentTask();
            if (!task.Schedule.Enabled)
                return null;

            var now = DateTime.Now;
            var today = new DateTime(now.Year, now.Month, now.Day, 
                task.Schedule.Hour, task.Schedule.Minute, 0);

            if (now < today)
            {
                return today;
            }
            else
            {
                return today.AddDays(1);
            }
        }

        public void Dispose()
        {
            _timer?.Stop();
            _timer?.Dispose();
            // 不再需要取消事件订阅
        }
    }
}

