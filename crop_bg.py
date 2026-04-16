from PIL import Image
img = Image.open('Assets/Resources/UI/Generated/bg_game_main.png')
print('Original size:', img.size)
# bg_texture in Pencil: x=-74, y=-111 in 1920x1080 frame
# Frame shows texture from pixel (74, 111) to (1994, 1191)
crop = img.crop((74, 111, 1994, 1191))
print('Cropped size:', crop.size)
crop.save('Assets/Resources/UI/Generated/bg_game_main.png')
print('Saved OK')
