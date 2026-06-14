import time
import random
import math
import win32gui
import win32con
import win32api
import pyautogui
from PIL import ImageGrab

# --------------------- БАЗОВЫЕ (для 1920x1080) ---------------------
BASE_W, BASE_H = 1920, 1080

BASE_BUTTONS = [
    (133, 133), (153, 183), (156, 227), (130, 273), (136, 318),
    (114, 365), (120, 410), (133, 452), (121, 496), (150, 544), (170, 590)
]

# Область объявлений (baseline)
BASE_AD_ZONE_X1, BASE_AD_ZONE_Y1 = 314, 125
BASE_AD_ZONE_X2, BASE_AD_ZONE_Y2 = 1881, 988

# Центр и иконка (для pyautogui.click — screen coords)
BASE_CENTER_X, BASE_CENTER_Y = 960, 540
BASE_ICON_X, BASE_ICON_Y = 1224, 167

# Область для проверки красного цвета (bbox in baseline coords)
BASE_WARN_BOX = (770, 436, 781, 439)
BASE_WARN_CLICK = (1084, 630)

# Координаты для детекции меню карты / паузы (single pixel)
BASE_MAP_PIXEL = (512, 120)

# Координаты для детекции состояний планшета (single pixel)
BASE_HUD_PIXEL = (1888, 25)
BASE_MP_PIXEL = (1770, 34)
# ---------------------------------------------------------------

# Эти переменные будут заполнены функцией apply_scaling(hwnd)
BUTTONS = []
AD_ZONE_X1 = AD_ZONE_Y1 = AD_ZONE_X2 = AD_ZONE_Y2 = 0
CENTER_X = CENTER_Y = ICON_X = ICON_Y = 0
WARN_BOX = (0, 0, 0, 0)
WARN_CLICK = (0, 0)
MAP_PIXEL_X = MAP_PIXEL_Y = 0
HUD_PIXEL = (0, 0)
MP_PIXEL = (0, 0)

# Виртуальные коды клавиш
VK_W = 0x57
VK_A = 0x41
VK_S = 0x53
VK_C = 0x43
VK_ESCAPE = win32con.VK_ESCAPE

# Настройки задержек между нажатиями поворота (в секундах)
TURN_GAP_MEAN_FIRST = 5.0    # кд между A->S и S->C для первого поворота
TURN_GAP_MEAN_SECOND = 4.0   # кд для второго поворота (уменьшённый)
TURN_GAP_JITTER = 0.5        # ± джиттер

def find_gta_hwnd():
    target_titles = ["Grand Theft Auto V", "Majestic Multiplayer"]
    for title in target_titles:
        gta_hwnd = win32gui.FindWindow(None, title)
        if gta_hwnd:
            return gta_hwnd, title
    return None, None

def get_window_rect(hwnd):
    try:
        left, top, right, bottom = win32gui.GetWindowRect(hwnd)
        width = right - left
        height = bottom - top
        return left, top, width, height
    except Exception:
        return None

def scale_point_base_to_screen(base_x, base_y, win_left, win_top, win_w, win_h):
    """Scale a baseline point (relative to BASE_WxBASE_H) to screen coords based on window rect."""
    if win_w <= 0 or win_h <= 0:
        screen_w, screen_h = pyautogui.size()
        sx = screen_w / BASE_W
        sy = screen_h / BASE_H
        return int(round(base_x * sx)), int(round(base_y * sy))
    sx = win_w / BASE_W
    sy = win_h / BASE_H
    screen_x = win_left + int(round(base_x * sx))
    screen_y = win_top + int(round(base_y * sy))
    return screen_x, screen_y

def scale_point_base_to_client(base_x, base_y, win_w, win_h):
    """Scale baseline point to client coordinates (0..win_w, 0..win_h) for PostMessage usage."""
    if win_w <= 0 or win_h <= 0:
        screen_w, screen_h = pyautogui.size()
        sx = screen_w / BASE_W
        sy = screen_h / BASE_H
        return int(round(base_x * sx)), int(round(base_y * sy))
    sx = win_w / BASE_W
    sy = win_h / BASE_H
    client_x = int(round(base_x * sx))
    client_y = int(round(base_y * sy))
    return client_x, client_y

def scale_box_base_to_screen(box, win_left, win_top, win_w, win_h):
    x1, y1, x2, y2 = box
    sx = win_w / BASE_W if win_w > 0 else pyautogui.size()[0] / BASE_W
    sy = win_h / BASE_H if win_h > 0 else pyautogui.size()[1] / BASE_H
    screen_x1 = win_left + int(round(x1 * sx))
    screen_y1 = win_top + int(round(y1 * sy))
    screen_x2 = win_left + int(round(x2 * sx))
    screen_y2 = win_top + int(round(y2 * sy))
    return (screen_x1, screen_y1, screen_x2, screen_y2)

def apply_scaling(hwnd):
    """Заполняет глобальные координаты, масштабируя базовые под текущий размер окна/экрана."""
    global BUTTONS, AD_ZONE_X1, AD_ZONE_Y1, AD_ZONE_X2, AD_ZONE_Y2
    global CENTER_X, CENTER_Y, ICON_X, ICON_Y
    global WARN_BOX, WARN_CLICK, MAP_PIXEL_X, MAP_PIXEL_Y, HUD_PIXEL, MP_PIXEL

    rect = get_window_rect(hwnd)
    if rect:
        win_left, win_top, win_w, win_h = rect
    else:
        screen_w, screen_h = pyautogui.size()
        win_left, win_top, win_w, win_h = 0, 0, screen_w, screen_h

    BUTTONS = [scale_point_base_to_client(x, y, win_w, win_h) for (x, y) in BASE_BUTTONS]

    ad1 = scale_point_base_to_client(BASE_AD_ZONE_X1, BASE_AD_ZONE_Y1, win_w, win_h)
    ad2 = scale_point_base_to_client(BASE_AD_ZONE_X2, BASE_AD_ZONE_Y2, win_w, win_h)
    AD_ZONE_X1, AD_ZONE_Y1 = min(ad1[0], ad2[0]), min(ad1[1], ad2[1])
    AD_ZONE_X2, AD_ZONE_Y2 = max(ad1[0], ad2[0]), max(ad1[1], ad2[1])

    CENTER_X, CENTER_Y = scale_point_base_to_screen(BASE_CENTER_X, BASE_CENTER_Y, win_left, win_top, win_w, win_h)
    ICON_X, ICON_Y = scale_point_base_to_screen(BASE_ICON_X, BASE_ICON_Y, win_left, win_top, win_w, win_h)

    WARN_BOX = scale_box_base_to_screen(BASE_WARN_BOX, win_left, win_top, win_w, win_h)
    WARN_CLICK = scale_point_base_to_screen(BASE_WARN_CLICK[0], BASE_WARN_CLICK[1], win_left, win_top, win_w, win_h)

    map_px = scale_point_base_to_screen(BASE_MAP_PIXEL[0], BASE_MAP_PIXEL[1], win_left, win_top, win_w, win_h)
    MAP_PIXEL_X, MAP_PIXEL_Y = map_px

    hud_px = scale_point_base_to_screen(BASE_HUD_PIXEL[0], BASE_HUD_PIXEL[1], win_left, win_top, win_w, win_h)
    HUD_PIXEL = hud_px

    mp_px = scale_point_base_to_screen(BASE_MP_PIXEL[0], BASE_MP_PIXEL[1], win_left, win_top, win_w, win_h)
    MP_PIXEL = mp_px

    print(f"[i] Применено масштабирование: window={win_w}x{win_h} @({win_left},{win_top})")
    print(f"[i] CENTER={CENTER_X, CENTER_Y}, ICON={ICON_X, ICON_Y}")
    print(f"[i] WARN_BOX={WARN_BOX}, WARN_CLICK={WARN_CLICK}")
    print(f"[i] MAP_PIXEL={MAP_PIXEL_X, MAP_PIXEL_Y}, HUD_PIXEL={HUD_PIXEL}, MP_PIXEL={MP_PIXEL}")
    print(f"[i] AD_ZONE client coords: ({AD_ZONE_X1},{AD_ZONE_Y1}) - ({AD_ZONE_X2},{AD_ZONE_Y2})")
    print(f"[i] BUTTONS (client): {BUTTONS}")

def force_foreground(hwnd_target):
    try:
        win32gui.ShowWindow(hwnd_target, win32con.SW_RESTORE)
        win32gui.SetForegroundWindow(hwnd_target)
    except Exception:
        win32api.keybd_event(win32con.VK_MENU, 0, 0, 0)
        win32gui.SetForegroundWindow(hwnd_target)
        win32api.keybd_event(win32con.VK_MENU, 0, win32con.KEYEVENTF_KEYUP, 0)

def check_and_close_warning():
    img = ImageGrab.grab(bbox=WARN_BOX)
    for x in range(img.width):
        for y in range(img.height):
            r, g, b = img.getpixel((x, y))
            if r > 180 and g < 100 and b < 100:
                print(f"[!] ОБНАРУЖЕНО уведомление склада! Клик по {WARN_CLICK}...")
                pyautogui.click(WARN_CLICK[0], WARN_CLICK[1])
                time.sleep(1.0)
                return True
    return False

def check_and_close_map():
    img = ImageGrab.grab(bbox=(MAP_PIXEL_X, MAP_PIXEL_Y, MAP_PIXEL_X + 1, MAP_PIXEL_Y + 1))
    r, g, b = img.getpixel((0, 0))
    if r > 200 and g < 40 and 80 <= b <= 140:
        print("[!] Обнаружено случайное открытие меню карты! Закрываем (ESC)...")
        send_key_active(VK_ESCAPE, 0.1)
        time.sleep(1.0)
        return True
    return False

def smart_state_recovery():
    print("[~] Анализ состояния интерфейса...")

    img_hud = ImageGrab.grab(bbox=(HUD_PIXEL[0], HUD_PIXEL[1], HUD_PIXEL[0] + 1, HUD_PIXEL[1] + 1))
    r_hud, g_hud, b_hud = img_hud.getpixel((0, 0))

    img_mp = ImageGrab.grab(bbox=(MP_PIXEL[0], MP_PIXEL[1], MP_PIXEL[0] + 1, MP_PIXEL[1] + 1))
    r_mp, g_mp, b_mp = img_mp.getpixel((0, 0))

    if 15 <= r_mp <= 50 and 45 <= g_mp <= 90 and 85 <= b_mp <= 130:
        print("[*] Статус: Маркетплейс открыт, но перекрыт плашкой. Убираем плашку...")
        check_and_close_warning()
        return

    if 40 <= r_mp <= 85 and 110 <= g_mp <= 160 and 190 <= b_mp <= 245:
        print("[*] Статус: Маркетплейс активен. Все отлично.")
        return

    if r_hud >= 200 and g_hud <= 60 and 80 <= b_hud <= 170:
        print("[*] Статус: Персонаж в игре. Открываю планшет и маркетплейс...")
        send_key_active(win32con.VK_DOWN, 0.1)
        time.sleep(1.5)
        pyautogui.click(CENTER_X, CENTER_Y)
        time.sleep(1.0)
        pyautogui.click(ICON_X, ICON_Y)
        time.sleep(4.5)
        check_and_close_warning()
        return

    print(f"[?] Статус: Неопознан (HUD: {r_hud},{g_hud},{b_hud} | MP: {r_mp},{g_mp},{b_mp}). Пробую дефолтное открытие...")
    send_key_active(win32con.VK_DOWN, 0.1)
    time.sleep(1.5)
    pyautogui.click(CENTER_X, CENTER_Y)
    time.sleep(1.0)
    pyautogui.click(ICON_X, ICON_Y)
    time.sleep(4.5)
    check_and_close_warning()

def move_and_click_background(gta_hwnd, target_x, target_y):
    start_x, start_y = random.randint(100, 1800), random.randint(100, 900)
    steps = random.randint(15, 25)
    for i in range(steps):
        t = math.sin((i / float(steps - 1)) * math.pi / 2)
        curr_x = int(start_x + (target_x - start_x) * t)
        curr_y = int(start_y + (target_y - start_y) * t)
        win32gui.PostMessage(gta_hwnd, win32con.WM_MOUSEMOVE, 0, win32api.MAKELONG(curr_x, curr_y))
        time.sleep(0.01)

    final_x = target_x + random.randint(-4, 4)
    final_y = target_y + random.randint(-4, 4)
    final_lparam = win32api.MAKELONG(final_x, final_y)

    win32gui.PostMessage(gta_hwnd, win32con.WM_MOUSEMOVE, 0, final_lparam)
    time.sleep(0.05)
    win32gui.PostMessage(gta_hwnd, win32con.WM_LBUTTONDOWN, win32con.MK_LBUTTON, final_lparam)
    time.sleep(random.uniform(0.07, 0.15))
    win32gui.PostMessage(gta_hwnd, win32con.WM_LBUTTONUP, 0, final_lparam)

def send_key_active(vk_code, duration=0.1):
    scan_code = win32api.MapVirtualKey(vk_code, 0)
    win32api.keybd_event(vk_code, scan_code, 0, 0)
    time.sleep(duration)
    win32api.keybd_event(vk_code, scan_code, win32con.KEYEVENTF_KEYUP, 0)

def perform_turn_sequence(gap_mean):
    dur_a = random.uniform(0.08, 0.18)
    dur_s = random.uniform(0.08, 0.18)
    dur_c = random.uniform(0.08, 0.18)

    gap1 = random.uniform(max(0.01, gap_mean - TURN_GAP_JITTER), gap_mean + TURN_GAP_JITTER)
    gap2 = random.uniform(max(0.01, gap_mean - TURN_GAP_JITTER), gap_mean + TURN_GAP_JITTER)

    print(f"[*] Выполняю поворот: A({dur_a:.2f}s) -> pause {gap1:.2f}s -> S({dur_s:.2f}s) -> pause {gap2:.2f}s -> C({dur_c:.2f}s)")
    send_key_active(VK_A, dur_a)
    time.sleep(gap1)
    send_key_active(VK_S, dur_s)
    time.sleep(gap2)
    send_key_active(VK_C, dur_c)
    time.sleep(random.uniform(0.05, 0.15))

def main():
    gta_hwnd, title = find_gta_hwnd()
    if not gta_hwnd:
        print("[-] Окно игры не найдено!")
        return

    print(f"[+] Подключено к: {title}")

    print("\n[!] Первичная инициализация состояния...")
    user_hwnd = win32gui.GetForegroundWindow()
    force_foreground(gta_hwnd)
    time.sleep(1.0)

    apply_scaling(gta_hwnd)

    smart_state_recovery()
    if user_hwnd:
        force_foreground(user_hwnd)

    print("[*] Скрипт перешел в рабочий цикл.\n")
    last_idx = -1

    while True:
        if not win32gui.IsWindow(gta_hwnd):
            print("[-] Игра закрылась. Выход.")
            break

        is_in_ad = False

        # --- ФОНОВЫЕ ДЕЙСТВИЯ ---
        idx = random.choice([i for i in range(len(BUTTONS)) if i != last_idx])
        last_idx = idx
        print(f"\n[*] [Фон] Клик по категории №{idx + 1}")
        move_and_click_background(gta_hwnd, BUTTONS[idx][0], BUTTONS[idx][1])
        time.sleep(random.uniform(25, 30))

        ad_x, ad_y = random.randint(AD_ZONE_X1, AD_ZONE_X2), random.randint(AD_ZONE_Y1, AD_ZONE_Y2)
        print(f"[*] [Фон] Клик по объявлению (client coords): ({ad_x}, {ad_y})")
        move_and_click_background(gta_hwnd, ad_x, ad_y)
        is_in_ad = True
        time.sleep(random.uniform(25, 30))

        # --- АКТИВНЫЕ ДЕЙСТВИЯ ---
        print("[!] Перехват фокуса для ходьбы...")
        user_hwnd = win32gui.GetForegroundWindow()
        force_foreground(gta_hwnd)
        time.sleep(0.8)

        if is_in_ad:
            print("[*] Выход из объявления (ESC)...")
            send_key_active(VK_ESCAPE, 0.1)
            time.sleep(0.5)

        print("[*] Закрытие маркетплейса (ESC x2)...")
        send_key_active(VK_ESCAPE, 0.1)
        time.sleep(0.5)
        send_key_active(VK_ESCAPE, 0.1)
        time.sleep(1.0)

        check_and_close_map()

        walk_time = random.uniform(1.5, 2.5)

        # ПЕРВЫЙ проход вперёд
        print(f"[*] Иду вперед {walk_time:.2f} сек... (первый проход)")
        send_key_active(VK_W, walk_time)
        time.sleep(0.2)

        # Первый поворот (кд ≈ TURN_GAP_MEAN_FIRST)
        perform_turn_sequence(TURN_GAP_MEAN_FIRST)

        # ВТОРОЙ проход вперёд
        print(f"[*] Иду вперед {walk_time:.2f} сек... (второй проход)")
        send_key_active(VK_W, walk_time)
        time.sleep(0.2)

        # Второй поворот (кд уменьшён до TURN_GAP_MEAN_SECOND)
        perform_turn_sequence(TURN_GAP_MEAN_SECOND)

        # После второго разворота не идём вперёд — продолжаем обычный цикл (smart_state_recovery и т.д.)
        print("[*] Второй разворот выполнен — не продолжаю движение вперёд.")

        # Небольшая пауза перед дальнейшими проверками
        time.sleep(0.5)

        smart_state_recovery()

        if user_hwnd:
            print("[+] Возврат фокуса пользователю.")
            force_foreground(user_hwnd)

        main_sleep = random.uniform(180, 360)
        print(f"[Zzz] Цикл завершен. Сплю {main_sleep / 60:.2f} мин.")
        time.sleep(main_sleep)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n[-] Скрипт остановлен пользователем.")