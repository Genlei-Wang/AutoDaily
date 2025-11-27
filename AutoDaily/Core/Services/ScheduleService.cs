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

        public ScheduleService(TaskService taskService, Action<Task> onTaskTriggered)
        {
            _taskService = taskService;
            _onTaskTriggered = onTaskTriggered;

            // 不再监听解锁事件（按用户要求移除）

            // 启动定时器，每30秒检查一次
            _timer = new Timer(30000); // 30秒
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
                return;

            var now = DateTime.Now;
            var scheduleTime = new DateTime(now.Year, now.Month, now.Day, 
                task.Schedule.Hour, task.Schedule.Minute, 0);

            bool isTime = now >= scheduleTime;
            bool notRunToday = !task.LastRun.HasValue || task.LastRun.Value.Date != now.Date;

            if (isTime && notRunToday)
            {
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

