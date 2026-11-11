from pathlib import Path
p=Path('src/CatTransform.App/MainWindow.cs');s=p.read_text(encoding='utf-8')
needle='  var rulesPanel=Stack('
assert s.count(needle)==1
insert='''  var ruleList=new ListView{MaxHeight=180,MinHeight=70,ItemsSource=vm.Settings.Conversion.Rules.Select(r=>(r.Enabled?"启用 · ":"禁用 · ")+r.Name).ToArray()};
  ruleList.SelectionChanged+=(_,_)=>{if(ruleList.SelectedIndex>=0)singleRule.Text=vm.Settings.Conversion.Rules[ruleList.SelectedIndex].Id.ToString();};
  void ApplyRuleList(Rule[] items){var selected=ruleList.SelectedIndex;Update(vm.Settings.Conversion with{Rules=items});rules.Text=RulePackages.Export(items);ruleList.ItemsSource=items.Select(r=>(r.Enabled?"启用 · ":"禁用 · ")+r.Name).ToArray();if(items.Length>0)ruleList.SelectedIndex=Math.Min(Math.Max(selected,0),items.Length-1);}
  void MoveRule(int delta){var items=vm.Settings.Conversion.Rules.OrderBy(r=>r.Order).ThenBy(r=>r.Id).ToArray();int index=Array.FindIndex(items,r=>r.Id==vm.Settings.Conversion.Rules[ruleList.SelectedIndex].Id);int target=index+delta;if(target<0||target>=items.Length)return;(items[index],items[target])=(items[target],items[index]);ApplyRuleList(items.Select((r,i)=>r with{Order=i}).ToArray());ruleList.SelectedIndex=target;}
  var ruleActions=Row(Button("启用 / 禁用",()=>{if(ruleList.SelectedIndex<0)return;var items=vm.Settings.Conversion.Rules.ToArray();items[ruleList.SelectedIndex]=items[ruleList.SelectedIndex] with{Enabled=!items[ruleList.SelectedIndex].Enabled};ApplyRuleList(items);}),Button("上移",()=>{if(ruleList.SelectedIndex>=0)MoveRule(-1);}),Button("下移",()=>{if(ruleList.SelectedIndex>=0)MoveRule(1);}),Button("删除选中规则",async()=>{if(ruleList.SelectedIndex>=0&&await Confirm("删除规则","删除选中规则？已有历史快照不受影响。"))ApplyRuleList(vm.Settings.Conversion.Rules.Where((r,i)=>i!=ruleList.SelectedIndex).ToArray());}));
'''
s=s.replace(needle,insert+'  var rulesPanel=Stack(ruleList,ruleActions,',1)
p.write_text(s,encoding='utf-8')
