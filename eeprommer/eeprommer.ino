/*
 ╔════════════════════════════════════════════════════════════════════════[×]═╗
 ║ eprommer                                                                   ║
 ╟──────────────────────────────────────────────────────────────────────────┬─╢
 ║ Copyright (C) 2019 dbalster@gmail.com, all rights reserved.              │▲║
 ║                                                                          │░║
 ║ simple (e)eprom programmer for 27c64, 27c128, 27c256, 27c512 chips.      │█║
 ║                                                                          │░║
 ║                                                                          │▼║
 ╚══════════════════════════════════════════════════════════════════════════╧═╝
*/
#include <Arduino.h>
#include <avr/pgmspace.h>
#include <stdarg.h>

#define SR_DATA   10
#define SR_CLOCK  11
#define SR_LATCH  12
#define LED       13

enum RomType {
  TYPE_GENERIC_ROM=0,
  TYPE_WINBOND_27Cxxx=1,
  TYPE_ATMEL_28Cxxx=2,
  TYPE_STM_27Cxxx=3,
};

enum Command {
  CMD_PRINT = 1,
  CMD_SET_LED = 2,
  CMD_READ_BLOCK = 3,
  CMD_READ_NEXT = 4,
  CMD_READ_AGAIN = 5,
  CMD_WRITE_BLOCK = 6,
  CMD_WRITE_NEXT = 7,
  CMD_WRITE_AGAIN = 8,
  CMD_ERASE = 9,
};

void debugf(const char* text, ...)
{
  va_list va;
  char buffer[256];
  va_start(va,text);
  buffer[0]=CMD_PRINT;
  buffer[1] = vsprintf(buffer+2,text,va)-1;
  va_end(va);
  Serial.write(buffer,buffer[1]+2);
}

// enable CS by pulling A0(PC0) low
static const inline void chipSelect_LOW()
{
  PORTC &= ~_BV(PC0);
}

static const inline void chipSelect_HIGH()
{
  PORTC |= _BV(PC0);
}

static const inline void LED_ON()
{
  PORTB &= ~_BV(PB5);
}

static const inline void LED_OFF()
{
  PORTB |= _BV(PB5);
}

// 2=PD2
// 3=PD3
// 4=PD4
// 5=PD5
// 6=PD6
// 7=PD7
// 8=PB0
// 9=PB1

// PC0 == CS
// PC1 == OE

#define WE (1<<14)

void outputEnable_LOW()
{
  DDRD &= B00000011;  // D0..D7 -> input
  DDRB &= B11111100;
  PORTC &= ~_BV(PC1); // readPin -> LOW
}

void outputEnable_HIGH()
{
  DDRD |= B11111100;  // D0..D7 -> output
  DDRB |= B00000011;
  PORTC |= _BV(PC1);//  readpin -> HIGH
}

void fastShiftOut(uint8_t val)
{
  uint8_t i;
  for (i = 0; i < 8; i++)
  {
    if (!!(val & (1 << (7 - i))))
      PORTB |= _BV(PB2);
    else
      PORTB &= ~_BV(PB2); 
    PORTB |= _BV(PB3);  // 11, SR_CLOCK
    PORTB &= ~_BV(PB3); 
  }
}

// takes 44µs
void setAddress(uint16_t address)
{
  PORTB &= ~_BV(PB4); 
  fastShiftOut(address>>8);
  fastShiftOut(address&255);
  PORTB |= _BV(PB4);
  PORTB &= ~_BV(PB4); 
}

void poke_winbond(uint16_t address, uint8_t value)
{
  chipSelect_HIGH();
  setAddress(address);

  PORTD &= B00000011;  // digitalWrite D0..D7
  PORTB &= B11111100;
  PORTD |= (value<<2) & 0xFC;
  PORTB |= (value>>6) & 0x03;

  chipSelect_LOW();
  delayMicroseconds(95);
  chipSelect_HIGH();
}

void poke_atmel(uint16_t address, uint8_t value)
{
  chipSelect_HIGH();
  setAddress(address);
  outputEnable_HIGH();

  PORTD &= B00000011;  // digitalWrite D0..D7
  PORTB &= B11111100;
  PORTD |= (value<<2) & 0xFC;
  PORTB |= (value>>6) & 0x03;

  chipSelect_LOW();

  delayMicroseconds(1000);
/*
  unsigned int ns = 2000;
  __asm__ __volatile__ (
    "1: sbiw %0,1" "\n\t" // 2 cycles
    "brne 1b" : "=w" (ns) : "0" (ns) // 2 cycles
  );
  */
  chipSelect_HIGH();
  setAddress(address | WE);
  outputEnable_LOW();
}

void poke(uint16_t address, uint8_t value)
{
  outputEnable_HIGH();
  setAddress(address | WE);
  chipSelect_LOW();

  PORTD &= B00000011;  // digitalWrite D0..D7
  PORTB &= B11111100;
  PORTD |= (value<<2) & 0xFC;
  PORTB |= (value>>6) & 0x03;

  setAddress(address);
  delayMicroseconds(95-44);
  setAddress(address | WE);
  chipSelect_HIGH();
}

byte peek(uint16_t address)
{
  setAddress(address | WE);
  byte value = 0;
  value |= (PIND>>2) & 0x3F;
  value |= (PINB<<6) & 0xC0;
  return value;
}

int last;
void setup()
{
  Serial.begin(115200);

  DDRC |= B00000011;

  chipSelect_HIGH();

  PORTC &= ~_BV(PC1);

  pinMode(SR_CLOCK,OUTPUT);
  pinMode(SR_DATA,OUTPUT);
  pinMode(SR_LATCH,OUTPUT);
  pinMode(LED,OUTPUT);
  LED_ON();
  delay(500);
  LED_OFF();


  digitalWrite(SR_LATCH,LOW);
  digitalWrite(SR_DATA,LOW);
  digitalWrite(SR_CLOCK,LOW);

  debugf("EPROMMER Version 1.4\n");
  last = millis();
}

static byte buffer[512];

bool blink = false;
void loop()
{
  if (Serial.available()>0)
  {
    int command = Serial.read();
    switch(command)
    {
      case CMD_SET_LED:
      {
        while (Serial.available()<=0);
        int state = Serial.read();
        digitalWrite(LED,state);
        debugf("LED is now %d\n",state);
      }
      break;
      case CMD_READ_BLOCK:
      {
        while (Serial.available()<5);
        int lo  = Serial.read();
        int hi  = Serial.read();
        int icommand = Serial.read();
        int ilo = Serial.read();
        int ihi = Serial.read();
        icommand &= 255; icommand ^= 255;
        ilo &= 255; ilo ^= 255;
        ihi &= 255; ihi ^= 255;
        int block = (hi<<8) | lo;

        if ((command == icommand)
        && (lo==ilo)
        && (hi==ihi)
        )
        {
          LED_ON();
          outputEnable_LOW();
          chipSelect_LOW();
  
          buffer[0] = 3;
          buffer[1] = 4+64;
          buffer[2] = block&255;
          buffer[3] = block>>8;
          buffer[4] = 0;
          buffer[5] = 0;
          uint16_t addr = block*64;
          //debugf("addr: %04x\n",addr);
          int chksum = 0;
          for (uint16_t i=0; i<64; i++)
          {
            byte val = peek(addr+i);
            buffer[6+i]= val;
            chksum += val;
          }
          buffer[4] = chksum&255;
          buffer[5] = chksum>>8;
          chipSelect_HIGH();
          Serial.write(buffer,buffer[1]+2);
          LED_OFF();
        }
        else
        {
          debugf("FAILED to received command, ignoring\n");
        }
      }
      break;

      case CMD_ERASE:
      {
        LED_ON();
        outputEnable_HIGH();
        chipSelect_LOW();
        setAddress(0 | WE);
        delay(10);
        setAddress(0);
        chipSelect_HIGH();
        outputEnable_LOW();
        LED_OFF();
        debugf("EEPROM erased.");
      }
      break;

      case CMD_WRITE_BLOCK:
      {
        LED_ON();
        Serial.readBytes(buffer,64+5);
        int chk=0;
        for (int i=0; i<64; ++i)
        {
          chk += buffer[5+i];
        }
        int block = buffer[0] + (buffer[1]<<8);
        int mode  = buffer[2];

        void (*_poke)(uint16_t address, uint8_t value) = poke;
        if (mode == TYPE_ATMEL_28Cxxx) _poke = poke_atmel;
        if (mode == TYPE_WINBOND_27Cxxx) _poke = poke_winbond;

        //_poke = poke_atmel;

        
        int csum  = buffer[3] + (buffer[4]<<8);
        if (csum == chk)
        {
          {
            outputEnable_LOW();
            byte val=0;
            int base = (block * 64);
            for (int i=0; i<64; ++i)
            {
              _poke(base+i,buffer[5+i]);
            }
            outputEnable_LOW();
          }
          
          buffer[0] = CMD_WRITE_NEXT;
          buffer[1] = 0;
          Serial.write(buffer,buffer[1]+2);
        }
        else
        {
          buffer[0] = CMD_WRITE_AGAIN;
          buffer[1] = 0;
          Serial.write(buffer,buffer[1]+2);
        }
        LED_OFF();
      }
      break;
    }
  }
}
