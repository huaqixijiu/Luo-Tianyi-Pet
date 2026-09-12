# 常见国际节日标注

2026-09-13：在 .78 基础上增加9项中文标注。固定日期6项，星期规则3项；无新依赖、不写用户数据、不增加节气表体积。

## 日期依据

- [2026节日日期参考](https://www.timeanddate.com/holidays/us/2026)
- [母亲节：五月第二个星期日及地区差异](https://www.timeanddate.com/holidays/us/mothers-day)
- [父亲节：六月第三个星期日](https://www.timeanddate.com/holidays/us/fathers-day)
- [美国感恩节日期](https://www.opm.gov/policy-data-oversight/pay-leave/federal-holidays/)

万圣夜是10月31日，与11月1日万圣节分别标注。母亲／父亲节采用通行日期，不自动选择国家。工作休息日继续完全由用户规则决定。

## 验证

新增12项测试，覆盖固定日期、2099年边界、全部74年的三个星期规则每年恰好一次、父亲节与夏至并列、用户工作日及提醒不受影响。实际WPF截图覆盖6月、11月和12月周视图。

实际结果：Core双目标各586项通过；应用双目标编译0警告／错误；net48与发布版各20项WPF通过，截图已检查。已安装0.1.0.79，签名Valid、包Ok、102项安装哈希一致，正式进程响应。详见同目录 international-holidays-*.txt/json/png。
