import { mount } from '@vue/test-utils'
import { beforeEach, expect, it, vi } from 'vitest'
import { nextTick, ref } from 'vue'

const waveSurferState = vi.hoisted(() => ({
  seekTo: vi.fn(),
  playPause: vi.fn(),
  setTime: vi.fn(),
  play: vi.fn(),
  pause: vi.fn(),
  options: null,
}))

vi.mock('@meersagor/wavesurfer-vue', () => ({
  useWaveSurfer: vi.fn((_config) => {
    waveSurferState.options = _config.options

    return {
      waveSurfer: ref({
        seekTo: waveSurferState.seekTo,
        playPause: waveSurferState.playPause,
        setTime: waveSurferState.setTime,
        play: waveSurferState.play,
        pause: waveSurferState.pause,
      }),
      isReady: ref(true),
      isPlaying: ref(false),
      currentTime: ref(14),
      totalDuration: ref(120),
    }
  }),
}))

vi.mock('@/shared/api/client', () => ({
  getAuthenticatedFetchOptions: vi.fn(() => ({
    credentials: 'include',
    headers: { Authorization: 'Bearer access-token' },
  })),
}))

beforeEach(() => {
  vi.clearAllMocks()
  waveSurferState.options = null
})

it('streams the recording with authenticated fetch options', async () => {
  const { getAuthenticatedFetchOptions } = await import('@/shared/api/client')
  const SpecificationPlayer = (await import('./SpecificationPlayer.vue')).default

  mount(SpecificationPlayer, {
    props: {
      projectId: 'project 1',
      recording: { id: 'recording 1' },
    },
  })

  await nextTick()

  expect(waveSurferState.options.value).toEqual({
    url: '/api/projects/project%201/recordings/recording%201/stream',
    height: 72,
    waveColor: '#f7b1a7',
    progressColor: '#f1361d',
    cursorColor: '#111827',
    barWidth: 2,
    barGap: 2,
    barRadius: 2,
    fetchParams: {
      credentials: 'include',
      headers: { Authorization: 'Bearer access-token' },
    },
  })
  expect(getAuthenticatedFetchOptions).toHaveBeenCalledTimes(1)
})

it('seeks through the exposed method without starting playback', async () => {
  const SpecificationPlayer = (await import('./SpecificationPlayer.vue')).default

  const wrapper = mount(SpecificationPlayer, {
    props: {
      projectId: 'project-1',
      recording: { id: 'recording-1' },
    },
  })

  wrapper.vm.seek(14)

  expect(waveSurferState.setTime).toHaveBeenCalledWith(14)
  expect(waveSurferState.play).not.toHaveBeenCalled()
  expect(waveSurferState.playPause).not.toHaveBeenCalled()
})

it('does not highlight an unrelated transcript segment when the active source is filtered out', async () => {
  const SpecificationTranscript = (await import('./SpecificationTranscript.vue')).default

  const wrapper = mount(SpecificationTranscript, {
    props: {
      activeStartSeconds: 14,
      query: 'billing',
      segments: [
        { startSeconds: 14, endSeconds: 18, text: 'Corporate sign-in' },
        { startSeconds: 30, endSeconds: 34, text: 'Billing report' },
      ],
    },
  })

  expect(wrapper.findAll('.specification-transcript__segment')).toHaveLength(1)
  expect(wrapper.find('.specification-transcript__segment--active').exists()).toBe(false)
})
