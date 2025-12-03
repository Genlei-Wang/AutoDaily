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

            // 启动定时器，每10秒检查一次（更频繁检查，确保不错过时间点）
            _timer = new Timer(10000); // 10秒
            _timer.Elapsed += Timer_Elapsed;
            _timer.AutoReset = true;
            _timer.Start();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            CheckSchedule();
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

            // 检查是否到了预定时间（允许前后5分钟的窗口）
            bool isTimeWindow = Math.Abs((now - scheduleTime).TotalMinutes) <= 5;
            
            // 检查今天是否已经运行过
            bool notRunToday = !task.LastRun.HasValue || task.LastRun.Value.Date != now.Date;
            
            // 防止重复触发：如果上次触发时间与当前时间在同一分钟内，不触发
            bool notRecentlyTriggered = !_lastTriggerTime.HasValue || 
                (now - _lastTriggerTime.Value).TotalMinutes >= 1;

            if (isTimeWindow && notRunToday && notRecentlyTriggered)
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

