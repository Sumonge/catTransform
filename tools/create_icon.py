from PIL import Image,ImageDraw
from pathlib import Path
image=Image.new('RGBA',(256,256),(0,0,0,0));d=ImageDraw.Draw(image)
d.rounded_rectangle((4,4,252,252),radius=54,fill='#142D45')
d.polygon([(54,102),(57,38),(107,76),(151,76),(201,38),(204,102)],fill='#32D2C5')
d.rounded_rectangle((52,75,205,201),radius=47,fill='#32D2C5')
d.arc((75,97,179,181),195,350,fill='#142D45',width=14)
d.polygon([(183,138),(185,171),(154,163)],fill='#142D45')
d.arc((75,93,179,177),15,170,fill='white',width=14)
d.polygon([(73,136),(71,103),(102,111)],fill='white')
dest=Path('src/CatTransform.App/Assets');dest.mkdir(exist_ok=True)
image.save(dest/'catTransform.ico',sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
