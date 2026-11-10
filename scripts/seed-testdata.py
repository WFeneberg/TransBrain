#!/usr/bin/env python3
"""Seeds a running TransBrain dev stack with test data through the API.

Creates catalog entries, 5 drivers, 5 vehicles (with documents), 5 customers (with delivery
locations), 5 orders and 5 tours. All dates are relative to today, so "expires soon" and
"expired" cases stay meaningful. Refuses to run when vehicles already exist.

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

# key: (first name, last name, classes, licence valid until, linked to fahrer.user)
DRIVERS = {
    "huber": ("Franz", "Huber", ["B", "C1", "C", "CE"], day(1663), True),
    "mayr": ("Josef", "Mayr", ["B", "C", "CE"], day(1055), False),
    "yilmaz": ("Ayşe", "Yılmaz-Öztürk", ["B", "C1", "C", "CE"], day(21), False),
    "gruber": ("Lukas", "Gruber", ["B", "C1"], day(2317), False),
    "schoepf": ("Maria", "Schöpf", ["B", "C", "CE"], day(-10), False),
}

# Tractor payload is the whole combination's: tours check capacity against the tractor only.
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
                         transportInsurancePolicyNumbers="UQ-TR-55102; UQ-TR-55103"),
         documents=[("Tachograph", day(-355), day(374), "Kontrollgerät DTCO 4.1"),
                    ("UVV", day(-214), day(150), None)]),
    dict(licensePlate="RE-TB 102", type="Tractor", payloadKg=25000, loadMeters=13.6, nextInspectionDue=day(35),
         make="Mercedes-Benz", model="Actros L 1853 LS", manufacturedOn="2022-06-01", powerKw=390,
         vehicleClass="Tank", carrierGroup="HuterVils", driver="mayr",
         chassisNumber="WDB9634031L987602", engineNumber="OM471-LA-0002", lengthMm=6050, widthMm=2500,
         heightMm=3880, curbWeightKg=8200, grossWeightKg=20500, maxFifthWheelLoadKg=12000,
         notes="ADR-Zugmaschine für Tankverkehr. Fahrer mit ADR-Schein Tank erforderlich.",
         dataSheet="Getriebe: PowerShift 3\nNebenantrieb für Tankpumpe\nMirrorCam",
         equipment=equipment(hasDangerousGoodsEquipment=True, isDangerousGoodsApproved=True,
                             hasSmallLadder=True, tankVolumeLiters=650, chainTensionerCount=2),
         commercial=dict(purchasePrice=142900.00, purchasedOn="2022-06-20", purchaseOdometerKm=50,
                         firstRegisteredOn="2022-06-24", usefulLifeYears=7, usefulLifeKm=1000000,
                         costCenter="KST-4200", vehicleInsurer="Uniqa", vehicleInsurancePolicyNumbers="UQ-KFZ-1188",
                         transportInsurer="Allianz", transportInsurancePolicyNumbers="AZ-TR-300045"),
         documents=[("ADR", day(-345), day(20), "Bald fällig – Testfall Ablauf"),
                    ("Tachograph", day(-700), day(29), None),
                    ("CEMT", day(-313), day(51), "Genehmigung Nr. AT-CEMT-0815")]),
    dict(licensePlate="KE-HV 7", type="Tractor", payloadKg=24000, loadMeters=13.6, nextInspectionDue=day(294),
         isLicensePlateConfidential=True, make="Scania", model="R 500 A4x2NA", manufacturedOn="2024-09-10",
         powerKw=368, vehicleClass="Rental", carrierGroup="Rental", driver="yilmaz",
         chassisNumber="YS2R4X20005123403", engineNumber="DC13-155-0003", lengthMm=5900, widthMm=2550,
         heightMm=3990, curbWeightKg=7600, grossWeightKg=18000, maxFifthWheelLoadKg=10500,
         notes="Leihfahrzeug, Rückgabe nach Saisonende. Kennzeichen vertraulich (Testfall).",
         dataSheet="Opticruise 14-Gang, Euro 6",
         equipment=equipment(hasLargeLadder=True, tankVolumeLiters=500),
         commercial=dict(purchasePrice=0, costCenter="KST-9000", vehicleInsurer="Allianz",
                         vehicleInsurancePolicyNumbers="AZ-MIET-2024-17"),
         documents=[]),
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
    dict(licensePlate="RE-TB 802", type="SemiTrailer", payloadKg=28500, loadMeters=13.6, nextInspectionDue=day(20),
         make="Krone", model="Profi Liner SDP 27", manufacturedOn="2019-08-20",
         vehicleClass="Tarpaulin", carrierGroup="Regular", status="InWorkshop",
         chassisNumber="WKESDP27000802", lengthMm=13950, widthMm=2550, heightMm=4000,
         curbWeightKg=6500, grossWeightKg=35000,
         notes="Planenauflieger für Marmor-/Steintransporte, Hubdach.",
         dataSheet="Achsen: BPW ECO Plus, Liftachse 1\nCode XL Aufbau",
         equipment=equipment(hasPalletBasket=True, hasLiftAxle=True, interiorLengthMm=13620, interiorWidthMm=2480,
                             interiorHeightMm=2700, supportPoleCount=14, loadBarCount=6, lashingStrapCount=24,
                             palletCount=33, chainTensionerCount=8, marbleRackCount=4, marbleRackMaterial="Steel"),
         commercial=dict(purchasePrice=34500.50, purchasedOn="2019-09-01", purchaseOdometerKm=0,
                         firstRegisteredOn="2019-09-02", usefulLifeYears=9, usefulLifeKm=1000000,
                         costCenter="KST-4300", vehicleInsurer="Uniqa",
                         vehicleInsurancePolicyNumbers="UQ-KFZ-2002; UQ-KFZ-2003", transportInsurer="Uniqa",
                         transportInsurancePolicyNumbers="UQ-TR-60001"),
         documents=[("UVV", day(-405), day(-40), "Abgelaufen – Testfall")]),
]

# key: (customer body, delivery locations)
CUSTOMERS = {
    "becker": (dict(
        name="Natursteinhandel Becker KG", street="Industriestr. 45", postalCode="86899", city="Landsberg am Lech",
        country="DE", contactPerson="Thomas Becker", email="dispo@becker-naturstein.example",
        phone="+49 8191 12345-0", vatId="DE123456789", paymentTermDays=30,
        additional=dict(secondaryName="Becker Steinwerk Lager 2", secondaryStreet="Gewerbering 8",
                        secondaryPostalCode="86916", secondaryCity="Kaufering", secondaryCountry="DE",
                        foundedOn="1978-04-01", workerCount=42, salariedEmployeeCount=11, reimburseToll=True,
                        customerClass="Key",
                        comment="Entladung nur mit Stapler Kategorie 5 t. Gestelle werden getauscht.")),
        [location("Lager Landsberg", "Industriestr. 45", "86899", "Landsberg am Lech", "DE", "Herr Kranz",
                  "lager@becker-naturstein.example", "+49 8191 12345-20"),
         location("Lager Kaufering", "Gewerbering 8", "86916", "Kaufering", "DE", "Frau Sedlmeier", None,
                  "+49 8191 12345-30")]),
    "frische": (dict(
        name="Frischelogistik Süd GmbH", street="Kühlhausweg 3", postalCode="80939", city="München", country="DE",
        contactPerson="Sabine Leitner", email="avis@frischelogistik-sued.example", phone="+49 89 3216-0",
        vatId="DE987654321", paymentTermDays=14,
        additional=dict(foundedOn="2004-09-15", workerCount=180, salariedEmployeeCount=35, requiresPalletCheck=True,
                        excludeTollFromInvoice=True, customerClass="HuterVilsAll",
                        comment="Zeitfenster zwingend im Portal buchen. Palettentausch EUR 1:1, Kühlkette dokumentieren.")),
        [location("Kühlhaus München-Nord", "Kühlhausweg 3", "80939", "München", "DE", "Rampenleitung",
                  "rampe@frischelogistik-sued.example", "+49 89 3216-55")]),
    "tanklager": (dict(
        name="Tanklager Hall GmbH", street="Innufer 22", postalCode="6060", city="Hall in Tirol", country="AT",
        contactPerson="Ing. Markus Pfurtscheller", email="betrieb@tanklager-hall.example", phone="+43 5223 4567",
        vatId="ATU12345678", paymentTermDays=45,
        additional=dict(foundedOn="1965-01-01", workerCount=12, salariedEmployeeCount=6, reimburseToll=True,
                        customerClass="Key", comment="ADR-Einweisung beim Pförtner. Anmeldung 24 h vorher.")),
        [location("Tanklager Hall – Tor 2", "Innufer 22", "6060", "Hall in Tirol", "AT", "Schichtleiter",
                  "schicht@tanklager-hall.example", "+43 5223 4567-2")]),
    "holzbau": (dict(
        name="Holzbau Füssen GmbH & Co. KG", street="Ziegelwies 5", postalCode="87629", city="Füssen", country="DE",
        contactPerson="Andreas Schweiger", email="info@holzbau-fuessen.example", phone="+49 8362 9876",
        vatId="DE246813579", paymentTermDays=21,
        additional=dict(secondaryName="Holzbau Füssen Zweigniederlassung Tirol", secondaryStreet="Hauptstr. 3",
                        secondaryPostalCode="6600", secondaryCity="Reutte", secondaryCountry="AT",
                        foundedOn="1992-06-10", workerCount=65, salariedEmployeeCount=9, customerClass="Occasional",
                        comment="Lieferungen an wechselnde Baustellen. Kranentladung vor Ort.")),
        [location("Baustelle Wohnanlage Lermoos", "Unterdorf 18", "6631", "Lermoos", "AT", "Polier Hr. Falger", None,
                  "+43 664 1234567"),
         location("Abbundhalle Füssen", "Ziegelwies 5", "87629", "Füssen", "DE", "Halle",
                  "halle@holzbau-fuessen.example", "+49 8362 9876-14")]),
    "wagner": (dict(
        name="Druckerei Wagner GmbH", street="Raiffeisenstr. 9", postalCode="89231", city="Neu-Ulm", country="DE",
        contactPerson=None, email=None, phone=None, vatId=None, paymentTermDays=0), []),
}

# key: (consignor, consignee customer (delivered to its first location), cargo, kg, ldm, pickup, delivery, cancelled)
ORDERS = {
    "marble": (address("Marmorwerk Untersberg GmbH", "Fürstenbrunner Str. 12", "5082", "Grödig", "AT"), "becker",
               "Marmorplatten auf Stahlgestellen, 4 Gestelle", 24000, 13.6,
               (at(2, "07:00"), at(2, "10:00")), (at(2, "14:00"), at(2, "17:00")), False),
    "dairy": (address("Alpenmilch Reutte eGen", "Mühler Str. 7", "6600", "Reutte", "AT"), "frische",
              "Molkereiprodukte gekühlt +2 bis +6 °C, 33 EUR-Paletten", 18500, 13.2,
              (at(3, "05:00"), at(3, "06:30")), (at(3, "09:00"), at(3, "11:00")), False),
    "fuel": (address("Chemiepark Kempten AG", "Ursulasrieder Str. 90", "87437", "Kempten", "DE"), "tanklager",
             "Heizöl EL, UN 1202, Klasse 3 (ADR) – Tankzug", 22000, 13.6,
             (at(6, "08:00"), at(6, "12:00")), (at(7, "07:00"), at(7, "12:00")), False),
    "timber": (address("Holzbau Füssen GmbH & Co. KG", "Ziegelwies 5", "87629", "Füssen", "DE"), "holzbau",
               "Brettschichtholz-Träger, Überlänge, 12 Pakete", 9500, 8.4,
               (at(8, "06:00"), at(8, "09:00")), (at(8, "11:00"), at(8, "15:00")), False),
    "paper": (address("Papierfabrik Vils GmbH", "Fabrikstr. 1", "6682", "Vils", "AT"), "wagner",
              "Papierrollen, 18 Stück stehend", 21000, 12.0,
              (at(1, "13:00"), at(1, "15:00")), (at(1, "18:00"), at(2, "08:00")), True),
}

# (date offset, vehicle, driver, orders, final status)
TOURS = [
    (2, "RE-TB 101", "huber", ["marble"], "Completed"),
    (3, "RE-TB 101", "huber", ["dairy"], "InProgress"),
    (6, "RE-TB 102", "mayr", ["fuel"], "Planned"),
    (8, "KE-HV 7", "yilmaz", ["timber"], "Planned"),
    (9, "RE-TB 102", "gruber", [], "Planned"),
]


def seed(api, driver_subject):
    if api("GET", "/vehicles?pageSize=1")["totalCount"] > 0:
        sys.exit("The database already holds vehicles; seeding twice would duplicate drivers, customers and orders.")

    existing = {(kind, entry["code"]) for kind in {k for k, _, _ in CATALOG}
                for entry in api("GET", f"/vehicle-catalog/{kind}")}
    for kind, code, name in CATALOG:
        if (kind, code) not in existing:
            api("POST", f"/vehicle-catalog/{kind}", dict(code=code, name=name))

    drivers = {}
    for key, (first, last, classes, valid_until, linked) in DRIVERS.items():
        drivers[key] = api("POST", "/drivers", dict(
            firstName=first, lastName=last, licenseClasses=classes, licenseValidUntil=valid_until,
            externalUserId=driver_subject if linked else None))["id"]

    vehicles = {}
    for spec in VEHICLES:
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
    for key, (body, locations) in CUSTOMERS.items():
        customer = api("POST", "/customers", body)
        location_ids = [api("POST", f"/customers/{customer['id']}/locations", item)["id"] for item in locations]
        customers[key] = (body, location_ids)

    orders = {}
    for key, (consignor, customer, cargo, kg, ldm, pickup, delivery, cancelled) in ORDERS.items():
        body, location_ids = customers[customer]
        target = CUSTOMERS[customer][1][0] if location_ids else body
        consignee = address(target["name"], target["street"], target["postalCode"], target["city"], target["country"])
        order = api("POST", "/orders", dict(
            consignor=consignor, consignee=consignee, cargoDescription=cargo, cargoWeightKg=kg,
            cargoLoadMeters=ldm, pickupFrom=pickup[0], pickupTo=pickup[1], deliveryFrom=delivery[0],
            deliveryTo=delivery[1], consigneeLocationId=location_ids[0] if location_ids else None))
        if cancelled:
            api("POST", f"/orders/{order['id']}/cancel")
        orders[key] = order["id"]

    for offset, plate, driver, order_keys, status in TOURS:
        tour = api("POST", "/tours", dict(tourDate=day(offset), vehicleId=vehicles[plate], driverId=drivers[driver]))
        for key in order_keys:
            api("POST", f"/tours/{tour['id']}/orders", dict(transportOrderId=orders[key]))
        if status in ("InProgress", "Completed"):
            api("POST", f"/tours/{tour['id']}/start")
        if status == "Completed":
            api("POST", f"/tours/{tour['id']}/complete")

    print(f"Seeded {len(DRIVERS)} drivers, {len(VEHICLES)} vehicles, {len(CUSTOMERS)} customers, "
          f"{len(ORDERS)} orders and {len(TOURS)} tours.")


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--api", default="http://localhost:5265")
    parser.add_argument("--keycloak", default="https://localhost:8080")
    args = parser.parse_args()
    api = Api(args.api, login(args.keycloak, "admin.user", "admin"))
    seed(api, subject(login(args.keycloak, "fahrer.user", "fahrer")))


if __name__ == "__main__":
    main()
