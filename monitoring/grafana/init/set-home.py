#!/usr/bin/env python3
import os
import sys
import time
import json
import base64
from urllib import request, error

GRAFANA_URL = os.environ.get('GRAFANA_URL', 'http://grafana:3000')
ADMIN_USER = os.environ.get('GRAFANA_ADMIN_USER', 'admin')
ADMIN_PASSWORD = os.environ.get('GRAFANA_ADMIN_PASSWORD')
DASHBOARD_UID = os.environ.get('GRAFANA_DASHBOARD_UID', 'webapi-http-requests')

if not ADMIN_PASSWORD:
    print('GRAFANA_ADMIN_PASSWORD is not set', file=sys.stderr)
    sys.exit(1)

AUTH = base64.b64encode(f"{ADMIN_USER}:{ADMIN_PASSWORD}".encode()).decode()

def http_get(path):
    req = request.Request(GRAFANA_URL + path, headers={'Authorization': 'Basic ' + AUTH})
    return request.urlopen(req, timeout=5).read()

def http_put(path, data):
    body = json.dumps(data).encode()
    req = request.Request(GRAFANA_URL + path, data=body, headers={'Authorization': 'Basic ' + AUTH, 'Content-Type': 'application/json'})
    req.get_method = lambda: 'PUT'
    return request.urlopen(req, timeout=5).read()

def wait_for_grafana():
    for i in range(1, 61):
        try:
            resp = http_get('/api/health')
            obj = json.loads(resp)
            if obj.get('database'):
                print('Grafana healthy')
                return True
        except Exception as ex:
            print('Waiting for grafana...', ex)
        time.sleep(2)
    return False

def find_dashboard_id(uid):
    try:
        resp = http_get(f'/api/dashboards/uid/{uid}')
        obj = json.loads(resp)
        return obj.get('dashboard', {}).get('id')
    except error.HTTPError as he:
        if he.code == 404:
            return None
        raise

def set_home_dashboard(dashboard_id):
    body = { 'homeDashboardId': dashboard_id }
    return http_put('/api/org/preferences', body)

def main():
    if not wait_for_grafana():
        print('Grafana did not become ready in time', file=sys.stderr)
        sys.exit(1)

    for attempt in range(1, 31):
        dash_id = find_dashboard_id(DASHBOARD_UID)
        if dash_id:
            print(f'Found dashboard id: {dash_id}, setting as home')
            try:
                set_home_dashboard(dash_id)
                print('Home dashboard set successfully')
                return 0
            except Exception as ex:
                print('Failed to set home dashboard:', ex)
        else:
            print(f'Dashboard uid {DASHBOARD_UID} not found, retrying...')
        time.sleep(2)

    print('Failed to set home dashboard after retries', file=sys.stderr)
    return 1

if __name__ == '__main__':
    sys.exit(main())
