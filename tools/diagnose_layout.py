from pathlib import Path
p=Path('src/CatTransform.App/MainWindow.cs');s=p.read_text(encoding='utf-8')
backup=Path('artifacts/MainWindow-before-layout-diagnosis.cs');backup.write_text(s,encoding='utf-8')
s=s.replace('previewDrawer.Content=nav;root.Children.Add(previewDrawer);','root.Children.Add(nav);')
s=s.replace('return FixedQueue(page,list);','return page;')
s=s.replace('  AppWindow.SetIcon(Path.combine', '  AppWindow.SetIcon(Path.combine')
p.write_text(s,encoding='utf-8')
