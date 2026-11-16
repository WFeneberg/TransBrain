#!/usr/bin/env python3
"""Seeds a running TransBrain dev stack with test data through the API.

Seeds the default dev tenant (admin.user) with 5 drivers, 6 vehicles (with documents),
5 customers (with delivery locations), 10 part-load orders and 4 tours. The orders are sized so
that several share one vehicle: three tours already carry two orders each, and four orders stay
Draft to be added to the planned tours — made for testing tours with multiple orders. Every
address is a real one in Upper Bavaria, verified to geocode at house-number level. All dates are
relative to today. The second dev tenant (admin.zweite) stays empty. Refuses to run when the
tenant already holds vehicles.

Usage (AppHost running): python3 scripts/seed-testdata.py [--api URL] [--keycloak URL]
"""
import argparse
import base64
import datetime
import hashlib
import http.cookiejar
import json
import os
import re
import ssl
import sys
import urllib.error
import urllib.parse
import urllib.request

# The dev Keycloak serves the self-signed ASP.NET dev certificate.
SSL = ssl._create_unverified_context()
TODAY = datetime.date.today()


def day(offset):
    return (TODAY + datetime.timedelta(days=offset)).isoformat()


def at(offset, time):
    return f"{day(offset)}T{time}:00+01:00"


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None


def login(keycloak, username, password):
    """Authorization-code + PKCE login as the SPA client, since it has no direct grant."""
    endpoint = f"{keycloak}/realms/transbrain/protocol/openid-connect"
    redirect = "http://localhost:4200/"
    verifier = base64.urlsafe_b64encode(os.urandom(32)).rstrip(b"=").decode()
    challenge = base64.urlsafe_b64encode(hashlib.sha256(verifier.encode()).digest()).rstrip(b"=").decode()
    opener = urllib.request.build_opener(
        urllib.request.HTTPSHandler(context=SSL),
        urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()),
        NoRedirect)
    query = urllib.parse.urlencode(dict(
        client_id="transbrain-spa", redirect_uri=redirect, response_type="code", scope="openid",
        code_challenge=challenge, code_challenge_method="S256"))
    page = opener.open(f"{endpoint}/auth?{query}").read().decode()
    action = re.search(r'action="([^"]+)"', page).group(1).replace("&amp;", "&")
    try:
        opener.open(action, urllib.parse.urlencode(dict(username=username, password=password)).encode())
        sys.exit(f"Login as {username} failed (wrong password?).")
    except urllib.error.HTTPError as redirect_response:
        location = redirect_response.headers["Location"]
    code = urllib.parse.parse_qs(urllib.parse.urlparse(location).query)["code"][0]
    token = opener.open(f"{endpoint}/token", urllib.parse.urlencode(dict(
        grant_type="authorization_code", client_id="transbrain-spa", code=code,
        redirect_uri=redirect, code_verifier=verifier)).encode())
    return json.load(token)["access_token"]


def subject(token):
    payload = token.split(".")[1]
    return json.loads(base64.urlsafe_b64decode(payload + "=" * (-len(payload) % 4)))["sub"]


class Api:
    def __init__(self, base, token):
        self.base, self.token = base.rstrip("/") + "/api", token

    def __call__(self, method, path, body=None):
        request = urllib.request.Request(
            self.base + path,
            json.dumps(body).encode() if body is not None else None,
            {"Authorization": "Bearer " + self.token, "Content-Type": "application/json"},
            method=method)
        try:
            return json.load(urllib.request.urlopen(request))
        except urllib.error.HTTPError as error:
            sys.exit(f"{method} {path} -> {error.code}: {error.read().decode()}")


def equipment(**flags):
    defaults = dict(
        hasPartitionWall=False, hasPalletBasket=False, hasDangerousGoodsEquipment=False,
        isDangerousGoodsApproved=False, hasLiftAxle=False, hasDoubleDeck=False,
        hasDoubleDeckBeams=False, hasSmallLadder=False, hasLargeLadder=False)
    return defaults | flags


def address(name, street, postal_code, city, country):
    return dict(name=name, street=street, postalCode=postal_code, city=city, country=country)


def location(name, street, postal_code, city, country, contact, email, phone):
    return address(name, street, postal_code, city, country) | dict(contactPerson=contact, email=email, phone=phone)


CATALOG = [
    ("InsuranceCompany", "Allianz", "Allianz Versicherungs-AG"),
    ("InsuranceCompany", "Uniqa", "UNIQA Insurance Group"),
    ("CustomerClass", "Key", "Stammkunde"),
    ("CustomerClass", "Occasional", "Gelegenheitskunde"),
]

# key: (first name, last name, classes, licence valid until, linked to the tenant's driver login).
# Every licence is valid well past the seeded tour dates, so each driver can take a tour.
DRIVERS = {
    "huber": ("Franz", "Huber", ["B", "C1", "C", "CE"], day(1460), True),
    "mayr": ("Josef", "Mayr", ["B", "C", "CE"], day(1050), False),
    "yilmaz": ("Ayşe", "Yılmaz-Öztürk", ["B", "C1", "C", "CE"], day(730), False),
    "gruber": ("Lukas", "Gruber", ["B", "C", "CE"], day(2000), False),
    "schoepf": ("Maria", "Schöpf", ["B", "C", "CE"], day(365), False),
}

# Tractor payload is the whole combination's: tours check capacity against the tractor only.
# Every vehicle stays Available and every document valid, so each tractor can drive a tour.
VEHICLES = [
    dict(licensePlate="RE-TB 101", type="Tractor", payloadKg=25000, loadMeters=13.6, nextInspectionDue=day(141),
         make="MAN", model="TGX 18.510 4x2 BLS", manufacturedOn="2023-02-15", powerKw=375,
         vehicleClass="Tarpaulin", carrierGroup="Fixed", driver="huber",
         chassisNumber="WMA06XZZ3NP123401", engineNumber="D2676LF51-0001", lengthMm=6100, widthMm=2550,
         heightMm=3950, curbWeightKg=7900, grossWeightKg=19500, maxFifthWheelLoadKg=11500,
         notes="Fernverkehr Süddeutschland – Österreich. Standheizung, Retarder.",
         dataSheet="Getriebe: TipMatic 12-Gang\nTank: 580 l Diesel + 80 l AdBlue\nAbgasnorm: Euro 6e",
         equipment=equipment(hasPalletBasket=True, hasSmallLadder=True, tankVolumeLiters=580,
                             lashingStrapCount=12, chainTensionerCount=4),
         commercial=dict(purchasePrice=128500.00, purchasedOn="2023-03-01", purchaseOdometerKm=120,
                         firstRegisteredOn="2023-03-06", usefulLifeYears=6, usefulLifeKm=900000,
                         costCenter="KST-4100", vehicleInsurer="Allianz",
                         vehicleInsurancePolicyNumbers="AZ-KFZ-7781234", transportInsurer="Uniqa",
                         transportInsurancePolicyNumbers="UQ-TR-55102"),
         documents=[("Tachograph", day(-355), day(374), "Kontrollgerät DTCO 4.1"),
                    ("UVV", day(-214), day(150), None)]),
    dict(licensePlate="RE-TB 102", type="Tractor", payloadKg=24000, loadMeters=13.6, nextInspectionDue=day(210),
         make="Mercedes-Benz", model="Actros L 1853 LS", manufacturedOn="2022-06-01", powerKw=390,
         vehicleClass="Tarpaulin", carrierGroup="Regular", driver="mayr",
         chassisNumber="WDB9634031L987602", engineNumber="OM471-LA-0002", lengthMm=6050, widthMm=2500,
         heightMm=3880, curbWeightKg=8200, grossWeightKg=20500, maxFifthWheelLoadKg=12000,
         notes="Planenverkehr Oberbayern, Baustoffe und Getränke.",
         dataSheet="Getriebe: PowerShift 3\nMirrorCam",
         equipment=equipment(hasPalletBasket=True, hasSmallLadder=True, tankVolumeLiters=650,
                             lashingStrapCount=12, chainTensionerCount=2),
         commercial=dict(purchasePrice=142900.00, purchasedOn="2022-06-20", purchaseOdometerKm=50,
                         firstRegisteredOn="2022-06-24", usefulLifeYears=7, usefulLifeKm=1000000,
                         costCenter="KST-4200", vehicleInsurer="Uniqa", vehicleInsurancePolicyNumbers="UQ-KFZ-1188",
                         transportInsurer="Allianz", transportInsurancePolicyNumbers="AZ-TR-300045"),
         documents=[("Tachograph", day(-335), day(395), None),
                    ("UVV", day(-120), day(245), None)]),
    dict(licensePlate="RE-TB 103", type="Tractor", payloadKg=24000, loadMeters=13.6, nextInspectionDue=day(294),
         make="Scania", model="R 500 A4x2NA", manufacturedOn="2024-09-10", powerKw=368,
         vehicleClass="Refrigerated", carrierGroup="Fixed", driver="yilmaz",
         chassisNumber="YS2R4X20005123403", engineNumber="DC13-155-0003", lengthMm=5900, widthMm=2550,
         heightMm=3990, curbWeightKg=7600, grossWeightKg=18000, maxFifthWheelLoadKg=10500,
         notes="Zugmaschine für Kühlverkehre mit Auflieger RE-TB 801.",
         dataSheet="Opticruise 14-Gang, Euro 6",
         equipment=equipment(hasSmallLadder=True, tankVolumeLiters=500, lashingStrapCount=8),
         commercial=dict(purchasePrice=131200.00, purchasedOn="2024-10-01", purchaseOdometerKm=90,
                         firstRegisteredOn="2024-10-04", usefulLifeYears=6, usefulLifeKm=900000,
                         costCenter="KST-4100", vehicleInsurer="Allianz",
                         vehicleInsurancePolicyNumbers="AZ-KFZ-7781301"),
         documents=[("Tachograph", day(-250), day(480), None),
                    ("UVV", day(-60), day(305), None)]),
    dict(licensePlate="RE-TB 104", type="Tractor", payloadKg=25000, loadMeters=13.6, nextInspectionDue=day(180),
         make="DAF", model="XG 480 FT", manufacturedOn="2024-03-11", powerKw=355,
         vehicleClass="Tarpaulin", carrierGroup="HuterVils", driver="gruber",
         chassisNumber="XLRTEH4300G201104", engineNumber="MX13-355-0104", lengthMm=6200, widthMm=2550,
         heightMm=3980, curbWeightKg=8100, grossWeightKg=19500, maxFifthWheelLoadKg=11500,
         notes="Nahverkehr Oberbayern, Baustoffe und Stahl.",
         dataSheet="Getriebe: TraXon 12-Gang\nTank: 430 l Diesel + 85 l AdBlue",
         equipment=equipment(hasPalletBasket=True, tankVolumeLiters=430, lashingStrapCount=16,
                             chainTensionerCount=6),
         commercial=dict(purchasePrice=119000.00, purchasedOn="2024-04-02", purchaseOdometerKm=80,
                         firstRegisteredOn="2024-04-05", usefulLifeYears=6, usefulLifeKm=800000,
                         costCenter="KST-4100", vehicleInsurer="Uniqa",
                         vehicleInsurancePolicyNumbers="UQ-KFZ-2041"),
         documents=[("Tachograph", day(-300), day(430), None),
                    ("UVV", day(-40), day(325), None)]),
    dict(licensePlate="RE-TB 801", type="SemiTrailer", payloadKg=27000, loadMeters=13.6, nextInspectionDue=day(82),
         make="Schmitz Cargobull", model="S.KO COOL Smart", manufacturedOn="2021-04-12",
         vehicleClass="Refrigerated", carrierGroup="Fixed",
         chassisNumber="WSM00000003456801", lengthMm=13700, widthMm=2600, heightMm=4000,
         curbWeightKg=9000, grossWeightKg=36000,
         notes="Kühlauflieger mit Thermo King SLXi 300, Doppelstock.",
         dataSheet="Kühlaggregat: Thermo King SLXi 300\nTemperaturbereich: -25 °C bis +25 °C\nDatenlogger: TouchPrint",
         equipment=equipment(hasPartitionWall=True, hasDoubleDeck=True, hasDoubleDeckBeams=True, hasLiftAxle=True,
                             tankVolumeLiters=240, interiorLengthMm=13410, interiorWidthMm=2460,
                             interiorHeightMm=2650, loadBarCount=10, lashingStrapCount=20, palletCount=66),
         commercial=dict(purchasePrice=78900.00, purchasedOn="2021-05-01", purchaseOdometerKm=0,
                         firstRegisteredOn="2021-05-03", usefulLifeYears=10, usefulLifeKm=1200000,
                         costCenter="KST-4100", vehicleInsurer="Allianz",
                         vehicleInsurancePolicyNumbers="AZ-KFZ-7781299", transportInsurer="Uniqa",
                         transportInsurancePolicyNumbers="UQ-TR-55104"),
         documents=[("ATP", day(-544), day(550), "FRC-Klasse"),
                    ("TailLift", day(-282), day(82), None)]),
    dict(licensePlate="RE-TB 802", type="SemiTrailer", payloadKg=28500, loadMeters=13.6, nextInspectionDue=day(160),
         make="Krone", model="Profi Liner SDP 27", manufacturedOn="2019-08-20",
         vehicleClass="Tarpaulin", carrierGroup="Fixed",
         chassisNumber="WKESDP27000802", lengthMm=13950, widthMm=2550, heightMm=4000,
         curbWeightKg=6500, grossWeightKg=35000,
         notes="Planenauflieger für Marmor- und Steintransporte, Hubdach.",
         dataSheet="Achsen: BPW ECO Plus, Liftachse 1\nCode XL Aufbau",
         equipment=equipment(hasPalletBasket=True, hasLiftAxle=True, interiorLengthMm=13620, interiorWidthMm=2480,
                             interiorHeightMm=2700, supportPoleCount=14, loadBarCount=6, lashingStrapCount=24,
                             palletCount=33, chainTensionerCount=8, marbleRackCount=4, marbleRackMaterial="Steel"),
         commercial=dict(purchasePrice=34500.50, purchasedOn="2019-09-01", purchaseOdometerKm=0,
                         firstRegisteredOn="2019-09-02", usefulLifeYears=9, usefulLifeKm=1000000,
                         costCenter="KST-4300", vehicleInsurer="Uniqa",
                         vehicleInsurancePolicyNumbers="UQ-KFZ-2002", transportInsurer="Uniqa",
                         transportInsurancePolicyNumbers="UQ-TR-60001"),
         documents=[("UVV", day(-100), day(265), None)]),
]

# Every address below exists in OpenStreetMap inside Upper Bavaria at house-number level (verified
# against Nominatim with the geocoder's exact query format), so all locations geocode and all
# tours route even with the small self-hosted `oberbayern` extract. The company names are invented.
# key: (customer body, delivery locations) - orders deliver to a customer's FIRST location.
CUSTOMERS = {
    "becker": (dict(
        name="Natursteinhandel Becker KG", street="Rudolf-Diesel-Str. 5", postalCode="86899", city="Landsberg am Lech",
        country="DE", contactPerson="Thomas Becker", email="dispo@becker-naturstein.example",
        phone="+49 8191 12345-0", vatId="DE123456789", paymentTermDays=30,
        additional=dict(secondaryName="Becker Steinwerk Lager 2", secondaryStreet="Viktor-Frankl-Str. 12",
                        secondaryPostalCode="86916", secondaryCity="Kaufering", secondaryCountry="DE",
                        foundedOn="1978-04-01", workerCount=42, salariedEmployeeCount=11, reimburseToll=True,
                        customerClass="Key",
                        comment="Entladung nur mit Stapler Kategorie 5 t. Gestelle werden getauscht.")),
        [location("Lager Landsberg", "Rudolf-Diesel-Str. 5", "86899", "Landsberg am Lech", "DE", "Herr Kranz",
                  "lager@becker-naturstein.example", "+49 8191 12345-20"),
         location("Lager Kaufering", "Viktor-Frankl-Str. 12", "86916", "Kaufering", "DE", "Frau Sedlmeier",
                  "kaufering@becker-naturstein.example", "+49 8191 12345-30")]),
    "frische": (dict(
        name="Frischelogistik Süd GmbH", street="Maria-Probst-Str. 20", postalCode="80939", city="München", country="DE",
        contactPerson="Sabine Leitner", email="avis@frischelogistik-sued.example", phone="+49 89 3216-0",
        vatId="DE987654321", paymentTermDays=14,
        additional=dict(foundedOn="2004-09-15", workerCount=180, salariedEmployeeCount=35, requiresPalletCheck=True,
                        excludeTollFromInvoice=True, customerClass="HuterVilsAll",
                        comment="Zeitfenster zwingend im Portal buchen. Palettentausch EUR 1:1, Kühlkette dokumentieren.")),
        [location("Kühlhaus München-Nord", "Maria-Probst-Str. 20", "80939", "München", "DE", "Rampenleitung",
                  "rampe@frischelogistik-sued.example", "+49 89 3216-55")]),
    "baustoffe": (dict(
        name="Baustoffe Inntal GmbH", street="Äußere Münchener Str. 100", postalCode="83026", city="Rosenheim",
        country="DE", contactPerson="Georg Stadler", email="einkauf@baustoffe-inntal.example",
        phone="+49 8031 5500-0", vatId="DE811223344", paymentTermDays=30,
        additional=dict(secondaryName="Baustoffe Inntal Lager Kolbermoor", secondaryStreet="Carl-Jordan-Str. 10",
                        secondaryPostalCode="83059", secondaryCity="Kolbermoor", secondaryCountry="DE",
                        foundedOn="1986-03-01", workerCount=58, salariedEmployeeCount=14, reimburseToll=True,
                        customerClass="Key", comment="Anlieferung 6–15 Uhr, Staplerentladung.")),
        [location("Lager Kolbermoor", "Carl-Jordan-Str. 10", "83059", "Kolbermoor", "DE", "Herr Obermaier",
                  "lager@baustoffe-inntal.example", "+49 8031 5500-31"),
         location("Hauptlager Rosenheim", "Äußere Münchener Str. 100", "83026", "Rosenheim", "DE", "Warenannahme",
                  "annahme@baustoffe-inntal.example", "+49 8031 5500-20")]),
    "getraenke": (dict(
        name="Getränke Chiemgau GmbH", street="Wasserburger Str. 66", postalCode="83278", city="Traunstein",
        country="DE", contactPerson="Monika Aigner", email="dispo@getraenke-chiemgau.example",
        phone="+49 861 7788-0", vatId="DE822334455", paymentTermDays=14,
        additional=dict(foundedOn="1999-07-01", workerCount=34, salariedEmployeeCount=8, requiresPalletCheck=True,
                        customerClass="Key", comment="Leergut wird im Tausch mitgenommen.")),
        [location("Auslieferungslager Traunreut", "Werner-von-Siemens-Str. 10", "83301", "Traunreut", "DE",
                  "Lagerleitung", "lager@getraenke-chiemgau.example", "+49 8669 4455")]),
    "holzbau": (dict(
        name="Holzbau Werdenfels GmbH & Co. KG", street="Zugspitzstr. 20", postalCode="82467",
        city="Garmisch-Partenkirchen", country="DE", contactPerson="Andreas Schweiger",
        email="info@holzbau-werdenfels.example", phone="+49 8821 9876", vatId="DE246813579", paymentTermDays=21,
        additional=dict(foundedOn="1992-06-10", workerCount=65, salariedEmployeeCount=9,
                        customerClass="Occasional",
                        comment="Lieferungen an wechselnde Baustellen. Kranentladung vor Ort.")),
        [location("Baustelle Wohnanlage Murnau", "Kemmelallee 1", "82418", "Murnau am Staffelsee", "DE",
                  "Polier Hr. Falger", None, "+49 171 1234567"),
         location("Abbundhalle Garmisch", "Zugspitzstr. 20", "82467", "Garmisch-Partenkirchen", "DE", "Halle",
                  "halle@holzbau-werdenfels.example", "+49 8821 9876-14")]),
}

# Part loads sized so several orders share one vehicle (the tractors carry 24-25 t / 13.6 ldm).
# The first six ride in pairs on the seeded tours; steel, paper, packaging and granite stay
# Draft - each fits the day +5 tour's remaining capacity on its own, and all four together still
# fit the empty day +6 tour, so building tours with multiple orders can be tested both ways.
# key: (consignor, consignee customer (delivered to its first location), cargo, kg, ldm, pickup, delivery)
ORDERS = {
    "marble": (address("Marmorwerk Kiefersfelden GmbH", "Kufsteiner Str. 20", "83088", "Kiefersfelden", "DE"), "becker",
               "Marmorplatten auf Stahlgestellen, 2 Gestelle", 8200, 4.0,
               (at(2, "07:00"), at(2, "09:00")), (at(2, "11:00"), at(2, "14:00"))),
    "tiles": (address("Zementwerk Rohrdorf GmbH", "Sinning 1", "83101", "Rohrdorf", "DE"), "baustoffe",
              "Dachziegel auf EUR-Paletten, 18 Paletten", 7400, 4.5,
              (at(2, "09:30"), at(2, "11:00")), (at(2, "13:00"), at(2, "16:00"))),
    "dairy": (address("Alpenmilch Weilheim eG", "Münchener Str. 20", "82362", "Weilheim in Oberbayern", "DE"), "frische",
              "Molkereiprodukte gekühlt +2 bis +6 °C, 12 EUR-Paletten", 6500, 4.4,
              (at(3, "05:00"), at(3, "06:30")), (at(3, "08:00"), at(3, "10:00"))),
    "drinks": (address("Brauerei Weihenstephan Logistik GmbH", "Clemensänger-Ring 10", "85356", "Freising", "DE"),
               "getraenke", "Getränke in Kisten, 14 EUR-Paletten", 7800, 5.0,
               (at(3, "07:00"), at(3, "09:00")), (at(3, "11:00"), at(3, "13:00"))),
    "timber": (address("Holzbau Werdenfels GmbH & Co. KG", "Zugspitzstr. 20", "82467", "Garmisch-Partenkirchen", "DE"),
               "holzbau", "Brettschichtholz-Träger, 6 Pakete", 5600, 4.2,
               (at(5, "06:00"), at(5, "08:00")), (at(5, "09:30"), at(5, "12:00"))),
    "cement": (address("Zementwerk Rohrdorf GmbH", "Sinning 1", "83101", "Rohrdorf", "DE"), "baustoffe",
               "Sackzement auf EUR-Paletten, 20 Paletten", 8000, 4.0,
               (at(5, "08:30"), at(5, "10:00")), (at(5, "12:00"), at(5, "15:00"))),
    "steel": (address("Stahlhandel München-Nord GmbH", "Frankfurter Ring 105", "80807", "München", "DE"), "baustoffe",
              "Betonstahlmatten, 40 Bund", 6000, 3.0,
              (at(5, "09:00"), at(5, "11:00")), (at(5, "13:00"), at(5, "16:00"))),
    "paper": (address("Papierfabrik Schongau GmbH", "Friedrich-Haindl-Str. 10", "86956", "Schongau", "DE"), "getraenke",
              "Kartonagen und Etikettenrollen, 16 Paletten", 4800, 4.5,
              (at(6, "06:00"), at(6, "08:00")), (at(6, "10:00"), at(6, "12:00"))),
    "packaging": (address("Verpackungswerk Burghausen GmbH", "Haiminger Str. 1", "84489", "Burghausen", "DE"), "frische",
                  "Verpackungsfolien und Trays, 10 Paletten", 3200, 3.6,
                  (at(6, "08:00"), at(6, "10:00")), (at(6, "12:00"), at(6, "14:00"))),
    "granite": (address("Marmorwerk Kiefersfelden GmbH", "Kufsteiner Str. 20", "83088", "Kiefersfelden", "DE"), "becker",
                "Granitplatten auf Stahlgestellen, 3 Gestelle", 9000, 2.4,
                (at(6, "07:00"), at(6, "09:00")), (at(6, "11:00"), at(6, "14:00"))),
}

# (date offset, vehicle, driver, orders, final status). Two orders per started tour; the planned
# day +5 tour keeps 11,400 kg / 5.4 ldm free and the day +6 tour starts empty, so the four Draft
# orders above can be assigned to them in the UI.
TOURS = [
    (2, "RE-TB 101", "huber", ["marble", "tiles"], "Completed"),
    (3, "RE-TB 103", "yilmaz", ["dairy", "drinks"], "InProgress"),
    (5, "RE-TB 104", "gruber", ["timber", "cement"], "Planned"),
    (6, "RE-TB 102", "mayr", [], "Planned"),
]

# (admin login, driver login, catalog, drivers, vehicles, customers, orders, tours).
# One tenant is enough for now; the second dev tenant (admin.zweite) stays empty.
TENANTS = [
    (("admin.user", "admin"), ("fahrer.user", "fahrer"), CATALOG, DRIVERS, VEHICLES, CUSTOMERS, ORDERS, TOURS),
]


def seed(api, driver_subject, catalog, driver_specs, vehicle_specs, customer_specs, order_specs, tour_specs):
    if api("GET", "/vehicles?pageSize=1")["totalCount"] > 0:
        sys.exit("The database already holds vehicles; seeding twice would duplicate drivers, customers and orders.")

    existing = {(kind, entry["code"]) for kind in {k for k, _, _ in catalog}
                for entry in api("GET", f"/vehicle-catalog/{kind}")}
    for kind, code, name in catalog:
        if (kind, code) not in existing:
            api("POST", f"/vehicle-catalog/{kind}", dict(code=code, name=name))

    drivers = {}
    for key, (first, last, classes, valid_until, linked) in driver_specs.items():
        drivers[key] = api("POST", "/drivers", dict(
            firstName=first, lastName=last, licenseClasses=classes, licenseValidUntil=valid_until,
            externalUserId=driver_subject if linked else None))["id"]

    vehicles = {}
    for spec in vehicle_specs:
        body = dict(spec)
        documents, status, driver = body.pop("documents"), body.pop("status", None), body.pop("driver", None)
        body["defaultDriverId"] = drivers[driver] if driver else None
        vehicle = api("POST", "/vehicles", body)
        vehicles[spec["licensePlate"]] = vehicle["id"]
        for document_type, checked, valid_until, remark in documents:
            api("POST", f"/vehicles/{vehicle['id']}/documents", dict(
                documentType=document_type, lastCheckedOn=checked, validUntil=valid_until, remark=remark))
        if status:
            api("POST", f"/vehicles/{vehicle['id']}/status", dict(status=status))

    customers = {}
    for key, (body, locations) in customer_specs.items():
        customer = api("POST", "/customers", body)
        location_ids = [api("POST", f"/customers/{customer['id']}/locations", item)["id"] for item in locations]
        customers[key] = (body, location_ids)

    orders = {}
    for key, (consignor, customer, cargo, kg, ldm, pickup, delivery) in order_specs.items():
        body, location_ids = customers[customer]
        target = customer_specs[customer][1][0] if location_ids else body
        consignee = address(target["name"], target["street"], target["postalCode"], target["city"], target["country"])
        order = api("POST", "/orders", dict(
            consignor=consignor, consignee=consignee, cargoDescription=cargo, cargoWeightKg=kg,
            cargoLoadMeters=ldm, pickupFrom=pickup[0], pickupTo=pickup[1], deliveryFrom=delivery[0],
            deliveryTo=delivery[1], consigneeLocationId=location_ids[0] if location_ids else None))
        orders[key] = order["id"]

    for offset, plate, driver, order_keys, status in tour_specs:
        tour = api("POST", "/tours", dict(tourDate=day(offset), vehicleId=vehicles[plate], driverId=drivers[driver]))
        for key in order_keys:
            api("POST", f"/tours/{tour['id']}/orders", dict(transportOrderId=orders[key]))
        if status in ("InProgress", "Completed"):
            api("POST", f"/tours/{tour['id']}/start")
        if status == "Completed":
            api("POST", f"/tours/{tour['id']}/complete")

    return (f"{len(driver_specs)} drivers, {len(vehicle_specs)} vehicles, {len(customer_specs)} customers, "
            f"{len(order_specs)} orders and {len(tour_specs)} tours")


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--api", default="http://localhost:5265")
    parser.add_argument("--keycloak", default="https://localhost:8080")
    args = parser.parse_args()
    for admin, driver, *data in TENANTS:
        api = Api(args.api, login(args.keycloak, *admin))
        print(f"{admin[0]}: seeded {seed(api, subject(login(args.keycloak, *driver)), *data)}.")


if __name__ == "__main__":
    main()
