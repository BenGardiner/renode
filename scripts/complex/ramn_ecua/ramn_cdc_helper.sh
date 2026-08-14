#!/bin/bash

# Helper script for RAMN ECUA CDC Serial over USB/IP
# Usage: ./ramn_cdc_helper.sh {list|attach|detach|monitor}

set -e

case "$1" in
    list)
        usbip list -r 127.0.0.1
        ;;
    attach)
        sudo usbip attach -r 127.0.0.1 -b 1-1
        sleep 1
        ls -la /dev/ttyACM* 2>/dev/null || echo "No ttyACM device found"
        ;;
    detach)
        sudo usbip detach -p 0
        ;;
    monitor)
        tty=$(ls /dev/ttyACM* 2>/dev/null | head -1)
        if [ -z "$tty" ]; then
            echo "No CDC device found. Run: $0 attach"
            exit 1
        fi
        minicom -D "$tty" -b 115200
        ;;
    *)
        echo "Usage: $0 {list|attach|detach|monitor}"
        exit 1
        ;;
esac
