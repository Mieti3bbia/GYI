import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { firstValueFrom } from 'rxjs';
import { App } from './app';
import { routes } from './app.routes';
import { DemoSession } from './auth/demo-session';
import { RouteMap } from './map/route-map';

async function loginEditor(): Promise<string> {
  const response = firstValueFrom(TestBed.inject(DemoSession).login('editor', 'editor'));
  TestBed.inject(HttpTestingController)
    .expectOne({ method: 'POST', url: '/api/session' })
    .flush({ id: 'f0820e61-db30-478b-a0b1-8870abc7e00e' });
  return (await response).id;
}

const stopMetadata = {
  cap: '00144',
  price: 8.5,
  municipality: 'Roma',
  province: 'RM',
  sourceUrl: 'https://example.com/tappa',
};

function fillStopMetadata(fill: (id: string, value: string) => void): void {
  fill('stop-cap', stopMetadata.cap);
  fill('stop-price', String(stopMetadata.price));
  fill('stop-municipality', stopMetadata.municipality);
  fill('stop-province', stopMetadata.province);
  fill('stop-source-url', stopMetadata.sourceUrl);
}

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    })
      .overrideComponent(RouteMap, { set: { template: '' } })
      .compileComponents();
  });

  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('redirects the root to the itinerary map and shows an empty catalog', async () => {
    const harness = await RouterTestingHarness.create('/');
    TestBed.inject(HttpTestingController).expectOne('/api/itineraries/public').flush([]);
    harness.detectChanges();
    expect(TestBed.inject(Router).url).toBe('/home');
    expect(harness.routeNativeElement?.querySelector('h1')?.textContent).toBe(
      'L’Italia, tappa dopo tappa.',
    );
    expect(harness.routeNativeElement?.textContent).toContain('Nessun itinerario disponibile');
  });

  it('shows confirmed itineraries only and sends the selected visit order to the map', async () => {
    const harness = await RouterTestingHarness.create('/home');
    const http = TestBed.inject(HttpTestingController);
    const first = { id: 'first', title: 'Tra i borghi', status: 'confirmed' };
    const second = { id: 'second', title: 'Lungo la costa', status: 'confirmed' };
    http
      .expectOne('/api/itineraries/public')
      .flush([first, { id: 'draft', title: 'Proposta in attesa', status: 'pending' }, second]);
    const firstRequest = http.expectOne('/api/itineraries/public/first');
    harness.detectChanges();
    const menu = harness.routeNativeElement!.querySelector<HTMLSelectElement>('#home-itinerary')!;
    expect(Array.from(menu.options, (option) => option.textContent)).toEqual([
      'Tra i borghi',
      'Lungo la costa',
    ]);
    menu.value = second.id;
    menu.dispatchEvent(new Event('change', { bubbles: true }));
    expect(firstRequest.cancelled).toBe(true);
    const stops = [
      { id: '2', name: 'Porto Recanati', latitude: 43.43, longitude: 13.66 },
      { id: '1', name: 'Numana', latitude: 43.51, longitude: 13.62 },
    ];
    http
      .expectOne('/api/itineraries/public/second')
      .flush({ ...second, geographicArea: 'Marche', stops });
    harness.detectChanges();

    const map = harness.routeDebugElement!.query(By.directive(RouteMap))
      .componentInstance as RouteMap;
    expect(map.stops()).toEqual(stops);
    expect(map.area()).toBe('Marche');
    const firstStop = harness.routeNativeElement!.querySelector<HTMLButtonElement>('.stop')!;
    firstStop.click();
    harness.detectChanges();
    expect(map.activeStopId()).toBe('2');
  });

  it('shows the saved visit order and lets the editor confirm the proposal', async () => {
    await loginEditor();
    const itineraryId = '001dd95b-afde-41ec-b1db-701112bf34fa';
    const url = '/editor/' + itineraryId;
    const harness = await RouterTestingHarness.create(url);
    const http = TestBed.inject(HttpTestingController);
    const itinerary = {
      id: itineraryId,
      title: 'Percorso nelle Marche',
      theme: 'Arte',
      description: 'Un itinerario tra musei.',
      geographicArea: 'Marche',
      stops: [
        { id: '2', name: 'Seconda nel catalogo', municipality: null },
        { id: '1', name: 'Prima nel catalogo', municipality: null },
      ],
      segments: [],
    };
    const endpoint = '/api/itineraries/' + itineraryId;
    http.expectOne(endpoint).flush({ ...itinerary, status: 'pending' });
    harness.detectChanges();

    expect(TestBed.inject(Router).url).toBe(url);
    const stops = harness.routeNativeElement?.querySelectorAll('ol li');
    expect(Array.from(stops ?? [], (stop) => stop.textContent?.trim())).toEqual([
      'Seconda nel catalogo',
      'Prima nel catalogo',
    ]);
    const page = harness.routeNativeElement!;
    expect(page.textContent).toContain('In attesa di conferma');
    const findButton = (label: string) =>
      Array.from(page.querySelectorAll('button')).find(
        (button) => button.textContent?.trim() === label,
      );
    findButton('Conferma itinerario')!.click();
    http.expectOne({ method: 'PUT', url: endpoint + '/confirm' }).flush(null, {
      status: 204,
      statusText: 'No Content',
    });
    http.expectOne(endpoint).flush({ ...itinerary, status: 'confirmed' });
    harness.detectChanges();

    expect(page.textContent).toContain('Confermato');
    expect(findButton('Conferma itinerario')).toBeUndefined();
    expect(findButton('Modifica itinerario')).toBeUndefined();
  });

  it('updates itinerary details and previews a manually geolocated stop before saving', async () => {
    await loginEditor();
    const id = '001dd95b-afde-41ec-b1db-701112bf34fa';
    const endpoint = '/api/itineraries/' + id;
    const harness = await RouterTestingHarness.create('/editor/' + id);
    const http = TestBed.inject(HttpTestingController);
    const draft = {
      id,
      title: 'Titolo iniziale',
      theme: 'Storia',
      description: 'Descrizione iniziale',
      geographicArea: 'Marche',
      status: 'pending',
      stops: [],
    };
    http.expectOne(endpoint).flush(draft);
    harness.detectChanges();
    const page = harness.routeNativeElement!;
    const click = (label: string) => {
      const button = Array.from(page.querySelectorAll('button')).find(
        (element) => element.textContent?.trim() === label,
      );
      expect(button).toBeDefined();
      button!.click();
      harness.detectChanges();
    };
    const setField = (id: string, value: string) => {
      const field = page.querySelector<HTMLInputElement>('#' + id)!;
      field.value = value;
      field.dispatchEvent(new Event('input', { bubbles: true }));
      harness.detectChanges();
    };
    click('Modifica itinerario');
    http.expectOne('/api/stops').flush([]);
    harness.detectChanges();
    expect(page.querySelector<HTMLInputElement>('#title')?.value).toBe(draft.title);
    setField('title', 'Titolo aggiornato');
    setField('description', 'Descrizione aggiornata');
    click('Nuova tappa');
    setField('stop-name', 'Belvedere');
    setField('stop-description', 'Vista panoramica');
    setField('stop-latitude', '91');
    setField('stop-longitude', '13.5');
    fillStopMetadata(setField);
    click('Applica tappa');
    expect(page.textContent).toContain('Inserisci un valore da −90 a 90.');
    const map = harness.routeDebugElement!.query(By.directive(RouteMap))
      .componentInstance as RouteMap;
    expect(map.stops().length).toBe(0);
    setField('stop-latitude', '43.5');
    click('Applica tappa');
    const details = {
      name: 'Belvedere',
      description: 'Vista panoramica',
      latitude: 43.5,
      longitude: 13.5,
      ...stopMetadata,
    };
    expect(map.stops()[0]).toMatchObject(details);
    click('Salva modifiche');
    const update = http.expectOne({ method: 'PUT', url: endpoint });
    expect(update.request.body).toEqual({
      title: 'Titolo aggiornato',
      theme: 'Storia',
      description: 'Descrizione aggiornata',
      geographicArea: 'Marche',
      stops: [{ stopId: undefined, details }],
    });
    update.flush(null, { status: 204, statusText: 'No Content' });
    http.expectOne(endpoint).flush({
      ...draft,
      title: 'Titolo aggiornato',
      description: 'Descrizione aggiornata',
      stops: [{ id: '2', ...details }],
    });
    harness.detectChanges();
    expect(page.querySelector('h1')?.textContent).toContain('Titolo aggiornato');
    expect(page.textContent).toContain('Belvedere');

    click('Modifica itinerario');
    http.expectOne('/api/stops').flush([]);
    harness.detectChanges();
    click('Modifica');
    expect(page.querySelector<HTMLInputElement>('#stop-cap')?.value).toBe('00144');
    expect(page.querySelector<HTMLInputElement>('#stop-source-url')?.value).toBe(
      stopMetadata.sourceUrl,
    );
    setField('stop-price', '0');
    click('Applica tappa');
    click('Salva modifiche');
    const priceUpdate = http.expectOne({ method: 'PUT', url: endpoint });
    expect(priceUpdate.request.body.stops).toEqual([
      { stopId: '2', details: { ...details, price: 0 } },
    ]);
    priceUpdate.flush(null, { status: 204, statusText: 'No Content' });
    http.expectOne(endpoint).flush({ ...draft, stops: [{ id: '3', ...details, price: 0 }] });
  });

  it('creates a catalog stop from its own tab and accepts a manual stop in a new itinerary', async () => {
    const editor = await loginEditor();
    const harness = await RouterTestingHarness.create('/editor/' + editor);
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/itineraries/mine').flush([]);
    harness.detectChanges();
    const page = harness.routeNativeElement!;
    const click = (label: string) => {
      const button = Array.from(page.querySelectorAll('button')).find(
        (element) => element.textContent?.trim() === label,
      );
      expect(button).toBeDefined();
      button!.click();
      harness.detectChanges();
    };
    const fill = (id: string, value: string) => {
      const field = page.querySelector<HTMLInputElement>('#' + id)!;
      field.value = value;
      field.dispatchEvent(new Event('input', { bubbles: true }));
      harness.detectChanges();
    };
    const fillStop = (name: string) => {
      fill('stop-name', name);
      fill('stop-description', 'Panorama');
      fill('stop-latitude', '43.5');
      fill('stop-longitude', '13.5');
      fillStopMetadata(fill);
    };
    click('Inserisci tappa');
    click('Salva tappa');
    http.expectNone('/api/stops');
    expect(page.textContent).toContain('Inserisci il nome della tappa');
    fillStop('Tappa di catalogo');
    fill('stop-cap', '00000');
    fill('stop-price', '-1');
    fill('stop-source-url', 'ftp://example.com/tappa');
    click('Salva tappa');
    http.expectNone('/api/stops');
    expect(page.textContent).toContain('Inserisci un CAP di cinque cifre.');
    expect(page.textContent).toContain('Inserisci un prezzo non negativo');
    expect(page.textContent).toContain('Inserisci un URL completo');
    fillStopMetadata(fill);
    const details = {
      name: 'Tappa di catalogo',
      description: 'Panorama',
      latitude: 43.5,
      longitude: 13.5,
      ...stopMetadata,
    };
    const catalogStop = { ...details, id: 'catalog', canAddToItinerary: true };
    click('Salva tappa');
    const save = http.expectOne({ method: 'POST', url: '/api/stops' });
    expect(save.request.body).toEqual(details);
    save.flush(catalogStop, { status: 201, statusText: 'Created' });
    harness.detectChanges();
    expect(page.textContent).toContain('disponibile nel catalogo');

    click('Inserisci itinerario');
    http.expectOne('/api/stops').flush([catalogStop]);
    harness.detectChanges();
    for (const key of ['title', 'description', 'theme', 'geographicArea']) fill(key, 'Test');
    click('Nuova tappa');
    fillStop('Tappa del percorso');
    click('Applica tappa');
    click('Proponi itinerario');
    const create = http.expectOne({ method: 'POST', url: '/api/itineraries' });
    expect(create.request.body.stops).toEqual([
      { stopId: undefined, details: { ...details, name: 'Tappa del percorso' } },
    ]);
    create.flush({ id: 'new', status: 'pending' }, { status: 201, statusText: 'Created' });
    await harness.fixture.whenStable();
    http.expectOne('/api/itineraries/new').flush({
      id: 'new',
      title: 'Test',
      status: 'pending',
      stops: [{ id: 'private', ...details }],
    });
    harness.detectChanges();
    expect(
      Array.from(page.querySelectorAll('button')).find(
        (button) => button.textContent?.trim() === 'Conferma itinerario',
      )?.disabled,
    ).toBe(true);
  });

  it('requires a price when opening operator participation on a confirmed itinerary', async () => {
    await loginEditor();
    const id = '001dd95b-afde-41ec-b1db-701112bf34fa';
    const endpoint = '/api/itineraries/' + id;
    const harness = await RouterTestingHarness.create('/editor/' + id);
    const http = TestBed.inject(HttpTestingController);
    const detail = {
      id,
      title: 'Pubblicato',
      status: 'confirmed',
      acceptsEconomicStops: false,
      averageTripPrice: null,
      stops: [{ id: 'a' }, { id: 'b' }],
    };
    http.expectOne(endpoint).flush(detail);
    harness.detectChanges();
    const page = harness.routeNativeElement!;
    const checkbox = page.querySelector<HTMLInputElement>('input[type=checkbox]')!;
    checkbox.click();
    harness.detectChanges();
    const form = page.querySelector<HTMLFormElement>('.participation')!;
    const submit = () => {
      form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
      harness.detectChanges();
    };
    submit();
    http.expectNone(endpoint + '/participation');
    expect(page.textContent).toContain('Inserisci un prezzo valido');
    const price = page.querySelector<HTMLInputElement>('#average-price')!;
    price.value = '35.25';
    price.dispatchEvent(new Event('input', { bubbles: true }));
    submit();
    const update = http.expectOne({ method: 'PUT', url: endpoint + '/participation' });
    expect(update.request.body).toEqual({ acceptsEconomicStops: true, averageTripPrice: 35.25 });
    update.flush(null, { status: 204, statusText: 'No Content' });
    http
      .expectOne(endpoint)
      .flush({ ...detail, acceptsEconomicStops: true, averageTripPrice: 35.25 });
    harness.detectChanges();
    expect(page.querySelector<HTMLInputElement>('#average-price')?.value).toBe('35.25');
  });

  it.each([
    { scenario: 'without stops', expectedIds: [] },
    { scenario: 'with one stop', expectedIds: ['1'] },
    { scenario: 'with reordered stops', expectedIds: ['3', '1'] },
  ])('saves a draft $scenario and loads its detail from the URL', async ({ expectedIds }) => {
    const editorId = await loginEditor();
    const harness = await RouterTestingHarness.create('/editor/' + editorId);
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/itineraries/mine').flush([]);
    harness.detectChanges();

    const page = harness.routeNativeElement!;
    const click = (label: string) => {
      const button = Array.from(page.querySelectorAll('button')).find(
        (element) => (element.getAttribute('aria-label') ?? element.textContent?.trim()) === label,
      );
      expect(button).toBeDefined();
      button!.click();
      harness.detectChanges();
    };

    click('Inserisci itinerario');
    const catalog = [
      { id: '1', name: 'Museo', municipality: 'Ancona', canAddToItinerary: true },
      { id: '2', name: 'Abbazia', municipality: 'Fiastra', canAddToItinerary: true },
      { id: '3', name: 'Rocca', municipality: 'Urbino', canAddToItinerary: true },
    ];
    http.expectOne({ method: 'GET', url: '/api/stops' }).flush([
      ...catalog,
      {
        id: '4',
        name: 'Parco',
        municipality: null,
        description: 'Proposta in attesa',
        latitude: 43.5,
        longitude: 13.5,
        canAddToItinerary: false,
      },
    ]);
    harness.detectChanges();

    for (const id of ['title', 'theme', 'description', 'geographicArea']) {
      const field = page.querySelector<HTMLInputElement>('#' + id)!;
      field.value = 'Prova';
      field.dispatchEvent(new Event('input', { bubbles: true }));
    }
    const catalogMenu = page.querySelector<HTMLSelectElement>('#catalog-stop')!;
    catalogMenu.value = '4';
    catalogMenu.dispatchEvent(new Event('change', { bubbles: true }));
    harness.detectChanges();
    expect(page.textContent).toContain('Proposta in attesa');
    expect(page.textContent).toContain('Latitudine 43.5, longitudine 13.5');
    const addButton = Array.from(page.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Aggiungi tappa',
    );
    expect(addButton?.disabled).toBe(true);
    const addStop = (id: string) => {
      catalogMenu.value = id;
      catalogMenu.dispatchEvent(new Event('change', { bubbles: true }));
      harness.detectChanges();
      click('Aggiungi tappa');
    };
    if (expectedIds.length === 1) {
      addStop('1');
    }
    if (expectedIds.length === 2) {
      catalog.forEach((stop) => addStop(stop.id));
      click('Rimuovi: Abbazia');
      click('Sposta prima: Rocca');
    }
    click('Proponi itinerario');

    const create = http.expectOne({ method: 'POST', url: '/api/itineraries' });
    expect(create.request.body.stops).toEqual(expectedIds.map((stopId) => ({ stopId })));
    const itinerary = {
      id: '001dd95b-afde-41ec-b1db-701112bf34fa',
      title: 'Prova',
      status: 'pending',
    };
    create.flush(itinerary, { status: 201, statusText: 'Created' });
    await harness.fixture.whenStable();

    const savedStops = expectedIds.map((id) => catalog.find((stop) => stop.id === id)!);
    http.expectOne('/api/itineraries/' + itinerary.id).flush({
      ...itinerary,
      stops: savedStops,
      segments: [],
    });
    harness.detectChanges();
    expect(TestBed.inject(Router).url).toBe('/editor/' + itinerary.id);
    expect(Array.from(page.querySelectorAll('ol li'), (stop) => stop.textContent?.trim())).toEqual(
      savedStops.map((stop) => stop.name + ' — ' + stop.municipality),
    );
  });
});
