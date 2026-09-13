using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using LuoTianyiPet.Core;
using Button=System.Windows.Controls.Button;
using CheckBox=System.Windows.Controls.CheckBox;
using TextBox=System.Windows.Controls.TextBox;
using ComboBox=System.Windows.Controls.ComboBox;
namespace LuoTianyiPet.App;
internal sealed partial class PlannerWindow
{
    private void Edit(ReminderItem? original,bool calendar)
    {
        _editing=true;StackPanel panel=new(){Width=530};DockPanel head=new();var close=Action("×",()=>{_editing=false;Render();});DockPanel.SetDock(close,Dock.Right);head.Children.Add(close);head.Children.Add(Text(original==null?calendar?"新建事项":"新建提醒":calendar?"编辑事项":"编辑提醒",24));panel.Children.Add(head);
        TextBlock error=Text("",12);error.Name="EditorError";error.Foreground=System.Windows.Media.Brushes.Crimson;
        bool relative=original?.Relative==true;
        var mode=Row();var specified=Action("◷ 指定时间",()=>{});var countdown=Action("⌛ 倒计时",()=>{});specified.Width=250;countdown.Width=250;mode.Children.Add(specified);mode.Children.Add(countdown);if(!calendar)panel.Children.Add(mode);
        StackPanel form=new();panel.Children.Add(form);
        TextBox name=new(){Height=40,Name="ReminderTitle",Text=original?.Title??"",MaxLength=120,Margin=new Thickness(3)};
        // Legacy combined content is kept intact in the note field until the user explicitly edits it.
        string legacyNotes=original?.Notes??"";
        if(original?.Content is string merged){var line=merged.IndexOf('\n');if(line>=0)legacyNotes=merged.Substring(line+1);}
        TextBox notes=new(){Name="ReminderNotes",Text=legacyNotes,AcceptsReturn=false,TextWrapping=TextWrapping.NoWrap,Height=40,Margin=new Thickness(3),ToolTip=legacyNotes};
        DateTime start=original?.Start??(calendar?_date.Date.AddHours(Math.Min(23,DateTime.Now.Hour+1)):DateTime.Now.AddHours(1));
        DatePicker date=new(){Name="ReminderDate",SelectedDate=start.Date,DisplayDateStart=ReminderSchedule.MinimumDate,DisplayDateEnd=ReminderSchedule.MaximumDate,Width=260,Margin=new Thickness(3),VerticalContentAlignment=VerticalAlignment.Center};
        TextBox time=new(){Height=40,Name="ReminderTime",Text=start.ToString("HH:mm"),Width=110,Margin=new Thickness(3)};
        CheckBox noTime=new(){Name="NoTime",Content="不指定时间",IsChecked=original?.HasTime==false,Margin=new Thickness(12)};
        CheckBox multiple=new(){Name="MultipleDates",Content="多日期",IsChecked=original?.Repeat==ReminderRepeat.Dates,Margin=new Thickness(12)};
        ComboBox repeat=new(){Name="ReminderRepeat",ItemsSource=Repeats,SelectedIndex=(int)(original?.Repeat??ReminderRepeat.Once),Margin=new Thickness(3)};
        var checks=Days.Select(d=>new CheckBox{Content="周"+"日一二三四五六"[(int)d],IsChecked=original?.Weekdays.Contains(d)==true,Margin=new Thickness(5)}).ToArray();StackPanel weekdays=Row();foreach(var c in checks)weekdays.Children.Add(c);
        List<DateTime> selectedDates=original?.Dates.Count>0?original.Dates.ToList():[start.Date];
        Button picker=null!;picker=Action("",()=>{DateSelectionWindow w=new(selectedDates,date.SelectedDate??start){Owner=this};if(w.ShowDialog()==true){selectedDates=w.Selection.OrderBy(d=>d).ToList();UpdateDateSummary();}});picker.Name="ModifyDates";
        void UpdateDateSummary()=>picker.Content=selectedDates.Count<4?string.Join("、",selectedDates.Select(d=>d.ToString("M/d")))+" · 修改":$"已选{selectedDates.Count}天 · 修改";
        UpdateDateSummary();
        var enabled=new CheckBox{Name="CreateAlarm",Content="创建闹钟提醒",IsChecked=original?.Enabled??false,Style=(Style)FindResource("PlannerSwitch"),Margin=new Thickness(3,12,3,12)};
        var early=new CheckBox{Name="EarlyReminder",Content="提前提醒",IsChecked=original?.EarlyEnabled??false,Style=(Style)FindResource("PlannerSwitch"),Margin=new Thickness(3,12,3,12)};
        TextBox lead=new(){Height=40,Name="EarlyMinutes",Text=(original?.EarlyMinutes??30).ToString(),Width=80,Margin=new Thickness(3)};StackPanel leadRow=Row();leadRow.Children.Add(lead);leadRow.Children.Add(Text("分钟 · 到点仍会提醒",12));
        int seconds=original?.DurationSeconds??_service.Book.LastDurationSeconds;
        PlannerNumber hours=new("CountdownHours",seconds/3600,24),minutes=new("CountdownMinutes",seconds/60%60,59),secs=new("CountdownSeconds",seconds%60,59);
        StackPanel digits=Row();foreach(var tuple in new[]{("小时",hours),("分钟",minutes),("秒",secs)}){StackPanel column=new();var label=Text(tuple.Item1,12);label.TextAlignment=TextAlignment.Center;column.Children.Add(label);var digitCard=Card(tuple.Item2);digitCard.Background=PlannerTheme.Soft;column.Children.Add(digitCard);digits.Children.Add(column);}
        StackPanel quick=Row();foreach(var preset in new[]{(300,"5分钟"),(900,"15分钟"),(1800,"30分钟"),(3600,"1小时")}){var button=Action(preset.Item2,()=>{hours.Input.Text=(preset.Item1/3600).ToString("00");minutes.Input.Text=(preset.Item1/60%60).ToString("00");secs.Input.Text="00";});button.Width=120;quick.Children.Add(button);}
        void Field(string label,UIElement input){Grid row=new(){Margin=new Thickness(0,6,0,6)};row.ColumnDefinitions.Add(new(){Width=new GridLength(116)});row.ColumnDefinitions.Add(new());row.Children.Add(Text(label));Grid.SetColumn(input,1);row.Children.Add(input);form.Children.Add(row);}
        Button? save=null;
        void Update()
        {
            form.Children.Clear();error.Text="";
            specified.Background=!relative?PlannerTheme.Accent:System.Windows.Media.Brushes.White;specified.Foreground=!relative?System.Windows.Media.Brushes.White:System.Windows.Media.Brushes.MidnightBlue;
            countdown.Background=relative?PlannerTheme.Accent:System.Windows.Media.Brushes.White;countdown.Foreground=relative?System.Windows.Media.Brushes.White:System.Windows.Media.Brushes.MidnightBlue;
            // Detach reusable fields from previous rows before rebuilding their conditional layout.
            foreach(var element in new UIElement[]{name,date,time,notes,multiple,noTime,repeat,picker,enabled,early,leadRow,weekdays,quick,digits})
            {if(LogicalTreeHelper.GetParent(element) is System.Windows.Controls.Panel parent)parent.Children.Remove(element);}
            if(relative){var nameLabel=Text("提醒名称（可选）");nameLabel.Margin=new Thickness(3,16,3,6);form.Children.Add(nameLabel);form.Children.Add(name);var quickLabel=Text("快捷选择");quickLabel.Margin=new Thickness(3,18,3,6);form.Children.Add(quickLabel);form.Children.Add(quick);var timerLabel=Text("倒计时设置                         最长24小时");timerLabel.Margin=new Thickness(3,18,3,6);form.Children.Add(timerLabel);form.Children.Add(digits);}
            else
            {
                if(calendar)Field("事项名称 *",name);
                StackPanel dateRow=Row();dateRow.Children.Add(date);if(calendar)dateRow.Children.Add(multiple);Field("日期 *",dateRow);
                StackPanel timeRow=Row();timeRow.Children.Add(time);if(calendar)timeRow.Children.Add(noTime);Field("时间",timeRow);
                if(!calendar)Field("名称（可选）",name);
                if(!calendar || original?.Repeat is ReminderRepeat.Daily or ReminderRepeat.Weekly or ReminderRepeat.Workdays or ReminderRepeat.RestDays)Field("重复",repeat);
                bool multi=calendar?multiple.IsChecked==true:repeat.SelectedIndex==3;
                if(multi)Field("已选日期",picker);
                if(repeat.SelectedIndex==2)form.Children.Add(weekdays);
                time.IsEnabled=noTime.IsChecked!=true;
                enabled.IsEnabled=noTime.IsChecked!=true;
                if(noTime.IsChecked==true)enabled.IsChecked=false;
                if(calendar)Field("",enabled);
                if(noTime.IsChecked==true&&calendar)form.Children.Add(Text("设置时间后可创建闹钟提醒",12));
                if(!calendar || enabled.IsChecked==true){Field("",early);if(early.IsChecked==true)Field("提前时长",leadRow);}
                if(calendar)Field("备注（可选）",notes);
            }
            if(save!=null)save.Content=relative?"▶ 开始倒计时":"保存";
        }
        specified.Click+=(_,_)=>{relative=false;Update();};countdown.Click+=(_,_)=>{relative=true;Update();};
        multiple.Click+=(_,_)=>Update();noTime.Click+=(_,_)=>Update();enabled.Click+=(_,_)=>Update();early.Click+=(_,_)=>Update();repeat.SelectionChanged+=(_,_)=>Update();
        panel.Children.Add(error);
        save=Action("保存",async()=>
        {
            try
            {
                DateTime now=DateTime.Now;int duration=hours.Value*3600+minutes.Value*60+secs.Value;
                if(relative&&(hours.Value<0||hours.Value>24||minutes.Value<0||minutes.Value>59||secs.Value<0||secs.Value>59||duration<1||duration>86400))throw new ArgumentException("请输入大于0且不超过24:00:00的时长。");
                if(calendar&&string.IsNullOrWhiteSpace(name.Text))throw new ArgumentException("请填写事项名称。");
                TimeSpan clock=TimeSpan.Zero;if(!relative&&noTime.IsChecked!=true&&(!TimeSpan.TryParseExact(time.Text.Trim(),new[]{@"h\:mm",@"hh\:mm"},CultureInfo.InvariantCulture,out clock)||clock.TotalDays>=1))throw new ArgumentException("时间请输入00:00～23:59。");
                var rule=relative?ReminderRepeat.Once:calendar?(multiple.IsChecked==true?ReminderRepeat.Dates:original?.Repeat is ReminderRepeat.Daily or ReminderRepeat.Weekly or ReminderRepeat.Workdays or ReminderRepeat.RestDays?(ReminderRepeat)repeat.SelectedIndex:ReminderRepeat.Once):(ReminderRepeat)repeat.SelectedIndex;
                if(rule==ReminderRepeat.Dates&&selectedDates.Count==0)throw new ArgumentException("请至少选择一个日期。");
                int earlyMinutes=original?.EarlyMinutes??30;if(!relative&&early.IsChecked==true&&(!int.TryParse(lead.Text,out earlyMinutes)||earlyMinutes<1||earlyMinutes>10080))throw new ArgumentException("提前时长请输入1～10080分钟。");
                var at=relative?now.AddSeconds(duration):(rule==ReminderRepeat.Dates?selectedDates.Min().Date:date.SelectedDate??throw new ArgumentException("请选择日期。"))+clock;
                bool on=relative||!calendar||enabled.IsChecked==true&&noTime.IsChecked!=true;
                if(on&&rule==ReminderRepeat.Once&&at<=now)throw new ArgumentException("该时间已过去，请选择未来日期和时间。");
                ReminderItem item=new(){Id=original?.Id??Guid.NewGuid(),Calendar=calendar,Title=name.Text.Trim(),Notes=calendar?notes.Text:"",Content=null,Start=at,HasTime=!calendar||noTime.IsChecked!=true,Enabled=on,ReminderCreated=on||original?.ReminderCreated==true,EarlyEnabled=!relative&&early.IsChecked==true,EarlyMinutes=earlyMinutes,Relative=relative,DurationSeconds=relative?duration:original?.DurationSeconds??1500,Repeat=rule,Dates=selectedDates,Weekdays=Days.Where((d,n)=>checks[n].IsChecked==true).ToList(),ExcludedDates=original?.ExcludedDates.ToList()??[],CheckedThrough=now};
                await Execute(b=>{b.Occurrences.RemoveAll(o=>o.RuleId==item.Id);b.Items.RemoveAll(i=>i.Id==item.Id);b.Items.Add(item);if(relative)b.LastDurationSeconds=duration;});_editing=false;Render();
            }catch(Exception ex)when(ex is ArgumentException or System.IO.IOException or UnauthorizedAccessException){error.Text=ex is ArgumentException?ex.Message:"保存失败，原数据保留，请重试。";}
        });save.Name="SaveReminder";save.Width=150;
        DockPanel buttons=new(){LastChildFill=false,Margin=new Thickness(0,18,0,0)};DockPanel.SetDock(save,Dock.Right);buttons.Children.Add(Primary(save));var cancel=Action("取消",()=>{_editing=false;Render();});cancel.Width=120;DockPanel.SetDock(cancel,Dock.Right);buttons.Children.Add(cancel);
        if(original!=null){var delete=Action(calendar?"删除事项…":"删除提醒…",()=>ConfirmDelete([original.Id],null));delete.Name="DeleteGroup";DockPanel.SetDock(delete,Dock.Left);buttons.Children.Add(delete);if(calendar&&_occurrenceDate is DateTime day&&original.Repeat!=ReminderRepeat.Once){var one=Action("仅删除当天",()=>ConfirmDelete([original.Id],day));one.Name="DeleteOccurrence";buttons.Children.Add(one);}}
        panel.Children.Add(buttons);Update();ShowOverlay(panel);name.Focus();
    }
}
