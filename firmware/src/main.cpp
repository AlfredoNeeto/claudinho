// Claudinho - firmware da CYD (ESP32-2432S028R, ILI9341 320x240).
#include <Arduino.h>
#include <Arduino_GFX_Library.h>
#include <NimBLEDevice.h>
#include <freertos/stream_buffer.h>
#include <lvgl.h>

#include "claudinho_core.h"
#include "ui.h"

// Pinos da CYD (repositório witnessmenow/ESP32-Cheap-Yellow-Display)
constexpr int kBacklight = 21;
Arduino_DataBus* bus = new Arduino_ESP32SPI(2 /*DC*/, 15 /*CS*/, 14 /*SCK*/, 13 /*MOSI*/, 12 /*MISO*/, HSPI);
Arduino_GFX* gfx = new Arduino_ILI9341(bus, GFX_NOT_DEFINED /*RST*/, 1 /*paisagem*/);

constexpr int kW = 320, kH = 240;
uint16_t drawBuf[kW * 24];  // render parcial: a CYD não tem PSRAM

cl::State state;
cl::LineReader reader, bleReader;  // um leitor por origem: bytes do USB e do BLE nunca se misturam

// Link BLE (plano ble-link, door 1): Nordic UART Service, o mesmo fluxo de bytes da serial.
NimBLECharacteristic* bleTx = nullptr;
StreamBufferHandle_t bleRx = nullptr;  // o callback roda na task do NimBLE; só o loop() lê

struct RxWrite : NimBLECharacteristicCallbacks {
  void onWrite(NimBLECharacteristic* c, NimBLEConnInfo&) override {
    const NimBLEAttValue v = c->getValue();
    xStreamBufferSend(bleRx, v.data(), v.size(), 0);  // cheio: perde bytes, a linha sai inválida e é descartada (AC 4)
  }
} rxWrite;

void ble_init() {
  bleRx = xStreamBufferCreate(1024, 1);
  NimBLEDevice::init("Claudinho");
  NimBLEServer* server = NimBLEDevice::createServer();
  server->advertiseOnDisconnect(true);
  NimBLEService* nus = server->createService("6E400001-B5A3-F393-E0A9-E50E24DCCA9E");
  nus->createCharacteristic("6E400002-B5A3-F393-E0A9-E50E24DCCA9E", NIMBLE_PROPERTY::WRITE | NIMBLE_PROPERTY::WRITE_NR)
      ->setCallbacks(&rxWrite);
  bleTx = nus->createCharacteristic("6E400003-B5A3-F393-E0A9-E50E24DCCA9E", NIMBLE_PROPERTY::NOTIFY);
  NimBLEAdvertising* adv = NimBLEDevice::getAdvertising();
  adv->addServiceUUID(nus->getUUID());
  adv->enableScanResponse(true);  // antes do setName: o UUID de 128 bits ocupa o anúncio, o nome vai na scan response
  adv->setName("Claudinho");
  adv->start();
}

void flush(lv_display_t* d, const lv_area_t* a, uint8_t* px) {
  gfx->draw16bitRGBBitmap(a->x1, a->y1, (uint16_t*)px, lv_area_get_width(a), lv_area_get_height(a));
  lv_display_flush_ready(d);
}

void handle(const char* line, bool fromBle) {
  static const char kHello[] = "claudinho 1 " CLAUDINHO_BOARD "\n";
  cl::Line l;
  int ver = 0;
  switch (cl::parse_line(line, l, ver)) {
    case cl::Parse::Hello:  // responde só na origem que perguntou
      if (fromBle) bleTx->notify((const uint8_t*)kHello, sizeof kHello - 1);
      else Serial.print(kHello);
      break;
    case cl::Parse::Ok: cl::on_line(state, l, millis()); break;
    case cl::Parse::Version: cl::on_version(state, ver); break;
    case cl::Parse::Bad: break;  // descarta e mantém a tela (AC 20)
  }
}

void setup() {
  Serial.setRxBufferSize(1024);
  Serial.begin(115200);
  Serial.print("BOOT\n");

  pinMode(kBacklight, OUTPUT);
  digitalWrite(kBacklight, HIGH);
  gfx->begin(40000000);
  gfx->invertDisplay(true);  // este painel da CYD inverte as cores; sem isso o fundo sai branco e o laranja sai azul (AC 29)
  gfx->fillScreen(0x0000);

  lv_init();
  lv_tick_set_cb([]() -> uint32_t { return millis(); });
  lv_display_t* d = lv_display_create(kW, kH);
  lv_display_set_flush_cb(d, flush);
  lv_display_set_buffers(d, drawBuf, nullptr, sizeof drawBuf, LV_DISPLAY_RENDER_MODE_PARTIAL);
  ui_init();
  ble_init();
  Serial.printf("heap livre %u\n", (unsigned)ESP.getFreeHeap());  // o LVGL e o BLE dividem o heap de uma placa sem PSRAM
}

void loop() {
  while (Serial.available()) {
    if (reader.feed((char)Serial.read())) handle(reader.line(), false);
  }
  char c;
  while (xStreamBufferReceive(bleRx, &c, 1, 0) == 1) {
    if (bleReader.feed(c)) handle(bleReader.line(), true);
  }
  const uint32_t now = millis();
  cl::tick(state, now);
  ui_update(state, now);
  lv_timer_handler();
  delay(5);
}
